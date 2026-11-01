"""MIPS R4300i (VR4300) disassembler with a simple pseudo-C lifter.

Used for the "Code" view: game code at ROM 0x1000 is loaded to the header's
entry point, so we can disassemble it, find functions via JAL targets and
print each instruction as an equivalent C-like statement.
"""
from __future__ import annotations

import struct
from dataclasses import dataclass

GPR = ["zero", "at", "v0", "v1", "a0", "a1", "a2", "a3", "t0", "t1", "t2", "t3", "t4", "t5", "t6",
       "t7", "s0", "s1", "s2", "s3", "s4", "s5", "s6", "s7", "t8", "t9", "k0", "k1", "gp", "sp",
       "fp", "ra"]
COP0 = ["Index", "Random", "EntryLo0", "EntryLo1", "Context", "PageMask", "Wired", "Reserved7",
        "BadVAddr", "Count", "EntryHi", "Compare", "Status", "Cause", "EPC", "PRId", "Config",
        "LLAddr", "WatchLo", "WatchHi", "XContext", "Reserved21", "Reserved22", "Reserved23",
        "Reserved24", "Reserved25", "PErr", "CacheErr", "TagLo", "TagHi", "ErrorEPC", "Reserved31"]


def fpr(i: int) -> str:
    return f"f{i}"


@dataclass
class Instr:
    addr: int
    word: int
    mnemonic: str
    operands: str
    pseudo: str
    target: int | None = None  # branch/jump target
    is_call: bool = False
    ends_block: bool = False

    def text(self) -> str:
        return f"{self.mnemonic:<8} {self.operands}".rstrip()


def _s16(v: int) -> int:
    return v - 0x10000 if v & 0x8000 else v


def _hex(v: int) -> str:
    return f"-0x{-v:X}" if v < 0 else f"0x{v:X}"


SPECIAL = {
    0x00: "sll", 0x02: "srl", 0x03: "sra", 0x04: "sllv", 0x06: "srlv", 0x07: "srav",
    0x08: "jr", 0x09: "jalr", 0x0C: "syscall", 0x0D: "break", 0x0F: "sync",
    0x10: "mfhi", 0x11: "mthi", 0x12: "mflo", 0x13: "mtlo",
    0x14: "dsllv", 0x16: "dsrlv", 0x17: "dsrav",
    0x18: "mult", 0x19: "multu", 0x1A: "div", 0x1B: "divu",
    0x1C: "dmult", 0x1D: "dmultu", 0x1E: "ddiv", 0x1F: "ddivu",
    0x20: "add", 0x21: "addu", 0x22: "sub", 0x23: "subu", 0x24: "and", 0x25: "or",
    0x26: "xor", 0x27: "nor", 0x2A: "slt", 0x2B: "sltu", 0x2C: "dadd", 0x2D: "daddu",
    0x2E: "dsub", 0x2F: "dsubu", 0x30: "tge", 0x31: "tgeu", 0x32: "tlt", 0x33: "tltu",
    0x34: "teq", 0x36: "tne", 0x38: "dsll", 0x3A: "dsrl", 0x3B: "dsra", 0x3C: "dsll32",
    0x3E: "dsrl32", 0x3F: "dsra32",
}
ALU3 = {"add": "+", "addu": "+", "sub": "-", "subu": "-", "and": "&", "or": "|", "xor": "^",
        "dadd": "+", "daddu": "+", "dsub": "-", "dsubu": "-"}
REGIMM = {0x00: "bltz", 0x01: "bgez", 0x02: "bltzl", 0x03: "bgezl", 0x10: "bltzal",
          0x11: "bgezal", 0x12: "bltzall", 0x13: "bgezall"}
REGIMM_COND = {"bltz": "<", "bgez": ">=", "bltzl": "<", "bgezl": ">=", "bltzal": "<",
               "bgezal": ">=", "bltzall": "<", "bgezall": ">="}
OPS = {
    0x02: "j", 0x03: "jal", 0x04: "beq", 0x05: "bne", 0x06: "blez", 0x07: "bgtz",
    0x08: "addi", 0x09: "addiu", 0x0A: "slti", 0x0B: "sltiu", 0x0C: "andi", 0x0D: "ori",
    0x0E: "xori", 0x0F: "lui", 0x14: "beql", 0x15: "bnel", 0x16: "blezl", 0x17: "bgtzl",
    0x18: "daddi", 0x19: "daddiu", 0x1A: "ldl", 0x1B: "ldr",
    0x20: "lb", 0x21: "lh", 0x22: "lwl", 0x23: "lw", 0x24: "lbu", 0x25: "lhu", 0x26: "lwr",
    0x27: "lwu", 0x28: "sb", 0x29: "sh", 0x2A: "swl", 0x2B: "sw", 0x2C: "sdl", 0x2D: "sdr",
    0x2E: "swr", 0x2F: "cache", 0x30: "ll", 0x31: "lwc1", 0x34: "lld", 0x35: "ldc1",
    0x37: "ld", 0x38: "sc", 0x39: "swc1", 0x3C: "scd", 0x3D: "sdc1", 0x3F: "sd",
}
LOAD_TYPES = {"lb": "s8", "lbu": "u8", "lh": "s16", "lhu": "u16", "lw": "s32", "lwu": "u32",
              "ld": "s64", "ll": "s32", "lld": "s64", "lwl": "s32", "lwr": "s32", "ldl": "s64",
              "ldr": "s64"}
STORE_TYPES = {"sb": "s8", "sh": "s16", "sw": "s32", "sd": "s64", "sc": "s32", "scd": "s64",
               "swl": "s32", "swr": "s32", "sdl": "s64", "sdr": "s64"}
IMM_OPS = {"addi": "+", "addiu": "+", "daddi": "+", "daddiu": "+", "andi": "&", "ori": "|",
           "xori": "^"}
FMT = {16: "s", 17: "d", 20: "w", 21: "l"}
COP1_FN = {0x00: "add", 0x01: "sub", 0x02: "mul", 0x03: "div", 0x04: "sqrt", 0x05: "abs",
           0x06: "mov", 0x07: "neg", 0x08: "round.l", 0x09: "trunc.l", 0x0A: "ceil.l",
           0x0B: "floor.l", 0x0C: "round.w", 0x0D: "trunc.w", 0x0E: "ceil.w", 0x0F: "floor.w",
           0x20: "cvt.s", 0x21: "cvt.d", 0x24: "cvt.w", 0x25: "cvt.l"}
FCMP = ["f", "un", "eq", "ueq", "olt", "ult", "ole", "ule", "sf", "ngle", "seq", "ngl", "lt",
        "nge", "le", "ngt"]


def decode(word: int, addr: int) -> Instr:
    op = word >> 26
    rs = (word >> 21) & 31
    rt = (word >> 16) & 31
    rd = (word >> 11) & 31
    sa = (word >> 6) & 31
    fn = word & 63
    imm = word & 0xFFFF
    simm = _s16(imm)
    R = GPR

    if word == 0:
        return Instr(addr, word, "nop", "", "/* nop */")

    if op == 0:
        m = SPECIAL.get(fn)
        if m is None:
            return Instr(addr, word, ".word", f"0x{word:08X}", f"/* unknown 0x{word:08X} */")
        if m in ("sll", "srl", "sra", "dsll", "dsrl", "dsra", "dsll32", "dsrl32", "dsra32"):
            sh = sa + (32 if m.endswith("32") else 0)
            sym = "<<" if "ll" in m else ">>"
            return Instr(addr, word, m, f"{R[rd]}, {R[rt]}, {sa}", f"{R[rd]} = {R[rt]} {sym} {sh};")
        if m in ("sllv", "srlv", "srav", "dsllv", "dsrlv", "dsrav"):
            sym = "<<" if "ll" in m else ">>"
            return Instr(addr, word, m, f"{R[rd]}, {R[rt]}, {R[rs]}", f"{R[rd]} = {R[rt]} {sym} {R[rs]};")
        if m == "jr":
            ps = "return;" if rs == 31 else f"goto *{R[rs]};"
            return Instr(addr, word, m, R[rs], ps, ends_block=True)
        if m == "jalr":
            return Instr(addr, word, m, f"{R[rd]}, {R[rs]}" if rd != 31 else R[rs], f"(*{R[rs]})();",
                         is_call=True)
        if m in ("syscall", "break", "sync"):
            return Instr(addr, word, m, "", f"{m}();")
        if m in ("mfhi", "mflo"):
            return Instr(addr, word, m, R[rd], f"{R[rd]} = {m[2:]};")
        if m in ("mthi", "mtlo"):
            return Instr(addr, word, m, R[rs], f"{m[2:]} = {R[rs]};")
        if m in ("mult", "multu", "dmult", "dmultu"):
            return Instr(addr, word, m, f"{R[rs]}, {R[rt]}", f"lo/hi = {R[rs]} * {R[rt]};")
        if m in ("div", "divu", "ddiv", "ddivu"):
            return Instr(addr, word, m, f"{R[rs]}, {R[rt]}",
                         f"lo = {R[rs]} / {R[rt]}; hi = {R[rs]} % {R[rt]};")
        if m in ALU3:
            if m in ("addu", "or", "daddu") and rt == 0:
                return Instr(addr, word, "move", f"{R[rd]}, {R[rs]}", f"{R[rd]} = {R[rs]};")
            return Instr(addr, word, m, f"{R[rd]}, {R[rs]}, {R[rt]}",
                         f"{R[rd]} = {R[rs]} {ALU3[m]} {R[rt]};")
        if m == "nor":
            return Instr(addr, word, m, f"{R[rd]}, {R[rs]}, {R[rt]}", f"{R[rd]} = ~({R[rs]} | {R[rt]});")
        if m in ("slt", "sltu"):
            return Instr(addr, word, m, f"{R[rd]}, {R[rs]}, {R[rt]}", f"{R[rd]} = {R[rs]} < {R[rt]};")
        return Instr(addr, word, m, f"{R[rs]}, {R[rt]}", f"trap_if({R[rs]}, {R[rt]});")

    if op == 1:
        m = REGIMM.get(rt)
        if m is None:
            return Instr(addr, word, ".word", f"0x{word:08X}", f"/* unknown 0x{word:08X} */")
        tgt = (addr + 4 + (simm << 2)) & 0xFFFFFFFF
        call = "al" in m
        ps = (f"if ({R[rs]} {REGIMM_COND[m]} 0) " +
              (f"func_{tgt:08X}();" if call else f"goto L_{tgt:08X};"))
        return Instr(addr, word, m, f"{R[rs]}, 0x{tgt:08X}", ps, target=tgt, is_call=call)

    if op in (2, 3):
        tgt = ((addr + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2)
        if op == 3:
            return Instr(addr, word, "jal", f"func_{tgt:08X}", f"func_{tgt:08X}();", target=tgt,
                         is_call=True)
        return Instr(addr, word, "j", f"0x{tgt:08X}", f"goto L_{tgt:08X};", target=tgt,
                     ends_block=True)

    if op in (0x10, 0x11, 0x12):
        cop = op - 0x10
        if cop == 0:
            if rs == 0:
                return Instr(addr, word, "mfc0", f"{R[rt]}, {COP0[rd]}", f"{R[rt]} = cop0.{COP0[rd]};")
            if rs == 4:
                return Instr(addr, word, "mtc0", f"{R[rt]}, {COP0[rd]}", f"cop0.{COP0[rd]} = {R[rt]};")
            if rs == 16:
                names = {1: "tlbr", 2: "tlbwi", 6: "tlbwr", 8: "tlbp", 0x18: "eret"}
                m = names.get(fn, "cop0")
                return Instr(addr, word, m, "", f"{m}();", ends_block=(m == "eret"))
        if cop == 1:
            if rs in (0, 1, 2, 4, 5, 6):
                m = {0: "mfc1", 1: "dmfc1", 2: "cfc1", 4: "mtc1", 5: "dmtc1", 6: "ctc1"}[rs]
                if rs < 4:
                    return Instr(addr, word, m, f"{R[rt]}, {fpr(rd)}", f"{R[rt]} = {fpr(rd)};")
                return Instr(addr, word, m, f"{R[rt]}, {fpr(rd)}", f"{fpr(rd)} = {R[rt]};")
            if rs == 8:
                m = {0: "bc1f", 1: "bc1t", 2: "bc1fl", 3: "bc1tl"}.get(rt & 3, "bc1")
                tgt = (addr + 4 + (simm << 2)) & 0xFFFFFFFF
                cond = "!fcc" if m in ("bc1f", "bc1fl") else "fcc"
                return Instr(addr, word, m, f"0x{tgt:08X}", f"if ({cond}) goto L_{tgt:08X};", target=tgt)
            if rs in FMT:
                f = FMT[rs]
                fs, ft, fd = rd, rt, sa
                if fn >= 0x30:
                    c = FCMP[fn & 15]
                    return Instr(addr, word, f"c.{c}.{f}", f"{fpr(fs)}, {fpr(ft)}",
                                 f"fcc = {fpr(fs)} {c} {fpr(ft)};")
                m = COP1_FN.get(fn)
                if m is None:
                    return Instr(addr, word, ".word", f"0x{word:08X}", "/* cop1 */")
                if m in ("add", "sub", "mul", "div"):
                    sym = {"add": "+", "sub": "-", "mul": "*", "div": "/"}[m]
                    return Instr(addr, word, f"{m}.{f}", f"{fpr(fd)}, {fpr(fs)}, {fpr(ft)}",
                                 f"{fpr(fd)} = {fpr(fs)} {sym} {fpr(ft)};")
                return Instr(addr, word, f"{m}.{f}", f"{fpr(fd)}, {fpr(fs)}",
                             f"{fpr(fd)} = {m.split('.')[0]}({fpr(fs)});")
        return Instr(addr, word, f"cop{cop}", f"0x{word & 0x3FFFFFF:07X}", f"/* cop{cop} op */")

    m = OPS.get(op)
    if m is None:
        return Instr(addr, word, ".word", f"0x{word:08X}", f"/* unknown 0x{word:08X} */")
    if m in ("beq", "bne", "beql", "bnel"):
        tgt = (addr + 4 + (simm << 2)) & 0xFFFFFFFF
        if m == "beq" and rs == 0 and rt == 0:
            return Instr(addr, word, "b", f"0x{tgt:08X}", f"goto L_{tgt:08X};", target=tgt, ends_block=True)
        sym = "==" if m.startswith("beq") else "!="
        return Instr(addr, word, m, f"{R[rs]}, {R[rt]}, 0x{tgt:08X}",
                     f"if ({R[rs]} {sym} {R[rt]}) goto L_{tgt:08X};", target=tgt)
    if m in ("blez", "bgtz", "blezl", "bgtzl"):
        tgt = (addr + 4 + (simm << 2)) & 0xFFFFFFFF
        sym = "<=" if m.startswith("blez") else ">"
        return Instr(addr, word, m, f"{R[rs]}, 0x{tgt:08X}", f"if ({R[rs]} {sym} 0) goto L_{tgt:08X};",
                     target=tgt)
    if m == "lui":
        return Instr(addr, word, m, f"{R[rt]}, 0x{imm:X}", f"{R[rt]} = 0x{imm << 16:08X};")
    if m in IMM_OPS:
        val = simm if m in ("addi", "addiu", "daddi", "daddiu") else imm
        if m in ("addiu", "addi") and rs == 0:
            return Instr(addr, word, "li", f"{R[rt]}, {_hex(val)}", f"{R[rt]} = {_hex(val)};")
        return Instr(addr, word, m, f"{R[rt]}, {R[rs]}, {_hex(val)}",
                     f"{R[rt]} = {R[rs]} {IMM_OPS[m]} {_hex(val)};")
    if m in ("slti", "sltiu"):
        return Instr(addr, word, m, f"{R[rt]}, {R[rs]}, {_hex(simm)}", f"{R[rt]} = {R[rs]} < {_hex(simm)};")
    mem = f"{_hex(simm)}({R[rs]})"
    if m in LOAD_TYPES:
        return Instr(addr, word, m, f"{R[rt]}, {mem}",
                     f"{R[rt]} = *({LOAD_TYPES[m]}*)({R[rs]} + {_hex(simm)});")
    if m in STORE_TYPES:
        return Instr(addr, word, m, f"{R[rt]}, {mem}",
                     f"*({STORE_TYPES[m]}*)({R[rs]} + {_hex(simm)}) = {R[rt]};")
    if m in ("lwc1", "ldc1"):
        t = "f32" if m == "lwc1" else "f64"
        return Instr(addr, word, m, f"{fpr(rt)}, {mem}", f"{fpr(rt)} = *({t}*)({R[rs]} + {_hex(simm)});")
    if m in ("swc1", "sdc1"):
        t = "f32" if m == "swc1" else "f64"
        return Instr(addr, word, m, f"{fpr(rt)}, {mem}", f"*({t}*)({R[rs]} + {_hex(simm)}) = {fpr(rt)};")
    if m == "cache":
        return Instr(addr, word, m, f"0x{rt:X}, {mem}", f"cache_op(0x{rt:X}, {R[rs]} + {_hex(simm)});")
    return Instr(addr, word, m, "", f"/* {m} */")


def disassemble(data: bytes, rom_offset: int, ram_addr: int, count: int) -> list[Instr]:
    out = []
    end = min(len(data), rom_offset + count * 4)
    for p in range(rom_offset, end - 3, 4):
        w = struct.unpack_from(">I", data, p)[0]
        out.append(decode(w, ram_addr + (p - rom_offset)))
    return out


def find_functions(data: bytes, code_start: int, code_size: int, ram_base: int) -> list[int]:
    """Return RAM addresses of functions (JAL targets inside the code region)."""
    end = min(len(data), code_start + code_size)
    targets = {ram_base}
    lo, hi = ram_base, ram_base + (end - code_start)
    words = struct.unpack_from(f">{(end - code_start) // 4}I", data, code_start)
    for w in words:
        if (w >> 26) == 3:
            t = (ram_base & 0xF0000000) | ((w & 0x03FFFFFF) << 2)
            if lo <= t < hi:
                targets.add(t)
    return sorted(targets)


def function_to_pseudo_c(data: bytes, rom_offset: int, ram_addr: int, max_instrs: int = 4000) -> str:
    """Lift one function to pseudo-C (stops after the jr ra + delay slot)."""
    instrs = []
    end_at = None
    for i, ins in enumerate(disassemble(data, rom_offset, ram_addr, max_instrs)):
        instrs.append(ins)
        if end_at is not None and i >= end_at:
            break
        if ins.mnemonic == "jr" and ins.operands == "ra":
            end_at = i + 1
    labels = {ins.target for ins in instrs if ins.target is not None and not ins.is_call}
    lines = [f"void func_{ram_addr:08X}(void) {{"]
    for ins in instrs:
        if ins.addr in labels:
            lines.append(f"L_{ins.addr:08X}:")
        lines.append(f"    {ins.pseudo:<48} // {ins.addr:08X}: {ins.text()}")
    lines.append("}")
    return "\n".join(lines)
