namespace N64AssetStudio.Core;

public sealed record Instr(uint Addr, uint Word, string Mnemonic, string Operands, string Pseudo,
    uint? Target = null, bool IsCall = false, bool EndsBlock = false)
{
    public string Text => $"{Mnemonic,-8} {Operands}".TrimEnd();
}

/// <summary>MIPS R4300i disassembler with a literal pseudo-C translation.</summary>
public static class Mips
{
    static readonly string[] R = { "zero", "at", "v0", "v1", "a0", "a1", "a2", "a3", "t0", "t1", "t2", "t3", "t4", "t5", "t6",
        "t7", "s0", "s1", "s2", "s3", "s4", "s5", "s6", "s7", "t8", "t9", "k0", "k1", "gp", "sp", "fp", "ra" };
    static readonly string[] Cop0 = { "Index", "Random", "EntryLo0", "EntryLo1", "Context", "PageMask", "Wired", "Reserved7",
        "BadVAddr", "Count", "EntryHi", "Compare", "Status", "Cause", "EPC", "PRId", "Config", "LLAddr", "WatchLo", "WatchHi",
        "XContext", "Reserved21", "Reserved22", "Reserved23", "Reserved24", "Reserved25", "PErr", "CacheErr", "TagLo", "TagHi",
        "ErrorEPC", "Reserved31" };
    static readonly Dictionary<int, string> Special = new()
    {
        [0x00] = "sll", [0x02] = "srl", [0x03] = "sra", [0x04] = "sllv", [0x06] = "srlv", [0x07] = "srav", [0x08] = "jr", [0x09] = "jalr",
        [0x0C] = "syscall", [0x0D] = "break", [0x0F] = "sync", [0x10] = "mfhi", [0x11] = "mthi", [0x12] = "mflo", [0x13] = "mtlo",
        [0x14] = "dsllv", [0x16] = "dsrlv", [0x17] = "dsrav", [0x18] = "mult", [0x19] = "multu", [0x1A] = "div", [0x1B] = "divu",
        [0x1C] = "dmult", [0x1D] = "dmultu", [0x1E] = "ddiv", [0x1F] = "ddivu", [0x20] = "add", [0x21] = "addu", [0x22] = "sub",
        [0x23] = "subu", [0x24] = "and", [0x25] = "or", [0x26] = "xor", [0x27] = "nor", [0x2A] = "slt", [0x2B] = "sltu",
        [0x2C] = "dadd", [0x2D] = "daddu", [0x2E] = "dsub", [0x2F] = "dsubu", [0x30] = "tge", [0x31] = "tgeu", [0x32] = "tlt",
        [0x33] = "tltu", [0x34] = "teq", [0x36] = "tne", [0x38] = "dsll", [0x3A] = "dsrl", [0x3B] = "dsra", [0x3C] = "dsll32",
        [0x3E] = "dsrl32", [0x3F] = "dsra32",
    };
    static readonly Dictionary<string, string> Alu3 = new()
    {
        ["add"] = "+", ["addu"] = "+", ["sub"] = "-", ["subu"] = "-", ["and"] = "&", ["or"] = "|", ["xor"] = "^",
        ["dadd"] = "+", ["daddu"] = "+", ["dsub"] = "-", ["dsubu"] = "-",
    };
    static readonly Dictionary<int, string> RegImm = new()
    {
        [0x00] = "bltz", [0x01] = "bgez", [0x02] = "bltzl", [0x03] = "bgezl", [0x10] = "bltzal", [0x11] = "bgezal", [0x12] = "bltzall", [0x13] = "bgezall",
    };
    static readonly Dictionary<int, string> Ops = new()
    {
        [0x02] = "j", [0x03] = "jal", [0x04] = "beq", [0x05] = "bne", [0x06] = "blez", [0x07] = "bgtz", [0x08] = "addi", [0x09] = "addiu",
        [0x0A] = "slti", [0x0B] = "sltiu", [0x0C] = "andi", [0x0D] = "ori", [0x0E] = "xori", [0x0F] = "lui", [0x14] = "beql", [0x15] = "bnel",
        [0x16] = "blezl", [0x17] = "bgtzl", [0x18] = "daddi", [0x19] = "daddiu", [0x1A] = "ldl", [0x1B] = "ldr", [0x20] = "lb", [0x21] = "lh",
        [0x22] = "lwl", [0x23] = "lw", [0x24] = "lbu", [0x25] = "lhu", [0x26] = "lwr", [0x27] = "lwu", [0x28] = "sb", [0x29] = "sh",
        [0x2A] = "swl", [0x2B] = "sw", [0x2C] = "sdl", [0x2D] = "sdr", [0x2E] = "swr", [0x2F] = "cache", [0x30] = "ll", [0x31] = "lwc1",
        [0x34] = "lld", [0x35] = "ldc1", [0x37] = "ld", [0x38] = "sc", [0x39] = "swc1", [0x3C] = "scd", [0x3D] = "sdc1", [0x3F] = "sd",
    };
    static readonly Dictionary<string, string> LoadT = new()
    {
        ["lb"] = "s8", ["lbu"] = "u8", ["lh"] = "s16", ["lhu"] = "u16", ["lw"] = "s32", ["lwu"] = "u32", ["ld"] = "s64", ["ll"] = "s32",
        ["lld"] = "s64", ["lwl"] = "s32", ["lwr"] = "s32", ["ldl"] = "s64", ["ldr"] = "s64",
    };
    static readonly Dictionary<string, string> StoreT = new()
    {
        ["sb"] = "s8", ["sh"] = "s16", ["sw"] = "s32", ["sd"] = "s64", ["sc"] = "s32", ["scd"] = "s64", ["swl"] = "s32", ["swr"] = "s32",
        ["sdl"] = "s64", ["sdr"] = "s64",
    };
    static readonly Dictionary<string, string> ImmOps = new()
    {
        ["addi"] = "+", ["addiu"] = "+", ["daddi"] = "+", ["daddiu"] = "+", ["andi"] = "&", ["ori"] = "|", ["xori"] = "^",
    };
    static readonly Dictionary<int, string> Fmt = new() { [16] = "s", [17] = "d", [20] = "w", [21] = "l" };
    static readonly Dictionary<int, string> Cop1Fn = new()
    {
        [0x00] = "add", [0x01] = "sub", [0x02] = "mul", [0x03] = "div", [0x04] = "sqrt", [0x05] = "abs", [0x06] = "mov", [0x07] = "neg",
        [0x08] = "round.l", [0x09] = "trunc.l", [0x0A] = "ceil.l", [0x0B] = "floor.l", [0x0C] = "round.w", [0x0D] = "trunc.w",
        [0x0E] = "ceil.w", [0x0F] = "floor.w", [0x20] = "cvt.s", [0x21] = "cvt.d", [0x24] = "cvt.w", [0x25] = "cvt.l",
    };
    static readonly string[] FCmp = { "f", "un", "eq", "ueq", "olt", "ult", "ole", "ule", "sf", "ngle", "seq", "ngl", "lt", "nge", "le", "ngt" };

    static string Hex(int v) => v < 0 ? $"-0x{-v:X}" : $"0x{v:X}";
    static string F(int i) => $"f{i}";

    public static Instr Decode(uint w, uint addr)
    {
        int op = (int)(w >> 26), rs = (int)((w >> 21) & 31), rt = (int)((w >> 16) & 31), rd = (int)((w >> 11) & 31);
        int sa = (int)((w >> 6) & 31), fn = (int)(w & 63), imm = (int)(w & 0xFFFF), simm = (short)(w & 0xFFFF);
        Instr Unk() => new(addr, w, ".word", $"0x{w:X8}", $"/* unknown 0x{w:X8} */");
        uint Br() => addr + 4 + (uint)(simm << 2);
        if (w == 0) return new(addr, w, "nop", "", "/* nop */");

        if (op == 0)
        {
            if (!Special.TryGetValue(fn, out var m)) return Unk();
            switch (m)
            {
                case "sll" or "srl" or "sra" or "dsll" or "dsrl" or "dsra" or "dsll32" or "dsrl32" or "dsra32":
                    {
                        int sh = sa + (m.EndsWith("32") ? 32 : 0);
                        string sym = m.Contains("ll") ? "<<" : ">>";
                        return new(addr, w, m, $"{R[rd]}, {R[rt]}, {sa}", $"{R[rd]} = {R[rt]} {sym} {sh};");
                    }
                case "sllv" or "srlv" or "srav" or "dsllv" or "dsrlv" or "dsrav":
                    return new(addr, w, m, $"{R[rd]}, {R[rt]}, {R[rs]}", $"{R[rd]} = {R[rt]} {(m.Contains("ll") ? "<<" : ">>")} {R[rs]};");
                case "jr": return new(addr, w, m, R[rs], rs == 31 ? "return;" : $"goto *{R[rs]};", EndsBlock: true);
                case "jalr": return new(addr, w, m, rd != 31 ? $"{R[rd]}, {R[rs]}" : R[rs], $"(*{R[rs]})();", IsCall: true);
                case "syscall" or "break" or "sync": return new(addr, w, m, "", $"{m}();");
                case "mfhi" or "mflo": return new(addr, w, m, R[rd], $"{R[rd]} = {m[2..]};");
                case "mthi" or "mtlo": return new(addr, w, m, R[rs], $"{m[2..]} = {R[rs]};");
                case "mult" or "multu" or "dmult" or "dmultu": return new(addr, w, m, $"{R[rs]}, {R[rt]}", $"lo/hi = {R[rs]} * {R[rt]};");
                case "div" or "divu" or "ddiv" or "ddivu":
                    return new(addr, w, m, $"{R[rs]}, {R[rt]}", $"lo = {R[rs]} / {R[rt]}; hi = {R[rs]} % {R[rt]};");
                case "nor": return new(addr, w, m, $"{R[rd]}, {R[rs]}, {R[rt]}", $"{R[rd]} = ~({R[rs]} | {R[rt]});");
                case "slt" or "sltu": return new(addr, w, m, $"{R[rd]}, {R[rs]}, {R[rt]}", $"{R[rd]} = {R[rs]} < {R[rt]};");
            }
            if (Alu3.TryGetValue(m, out var sym3))
            {
                if (m is "addu" or "or" or "daddu" && rt == 0) return new(addr, w, "move", $"{R[rd]}, {R[rs]}", $"{R[rd]} = {R[rs]};");
                return new(addr, w, m, $"{R[rd]}, {R[rs]}, {R[rt]}", $"{R[rd]} = {R[rs]} {sym3} {R[rt]};");
            }
            return new(addr, w, m, $"{R[rs]}, {R[rt]}", $"trap_if({R[rs]}, {R[rt]});");
        }
        if (op == 1)
        {
            if (!RegImm.TryGetValue(rt, out var m)) return Unk();
            uint tgt = Br();
            bool call = m.Contains("al");
            string cond = m.StartsWith("bltz") ? "<" : ">=";
            return new(addr, w, m, $"{R[rs]}, 0x{tgt:X8}",
                $"if ({R[rs]} {cond} 0) " + (call ? $"func_{tgt:X8}();" : $"goto L_{tgt:X8};"), tgt, call);
        }
        if (op is 2 or 3)
        {
            uint tgt = ((addr + 4) & 0xF0000000) | ((w & 0x03FFFFFF) << 2);
            return op == 3
                ? new(addr, w, "jal", $"func_{tgt:X8}", $"func_{tgt:X8}();", tgt, true)
                : new(addr, w, "j", $"0x{tgt:X8}", $"goto L_{tgt:X8};", tgt, false, true);
        }
        if (op is >= 0x10 and <= 0x12)
        {
            int cop = op - 0x10;
            if (cop == 0)
            {
                if (rs == 0) return new(addr, w, "mfc0", $"{R[rt]}, {Cop0[rd]}", $"{R[rt]} = cop0.{Cop0[rd]};");
                if (rs == 4) return new(addr, w, "mtc0", $"{R[rt]}, {Cop0[rd]}", $"cop0.{Cop0[rd]} = {R[rt]};");
                if (rs == 16)
                {
                    string m = fn switch { 1 => "tlbr", 2 => "tlbwi", 6 => "tlbwr", 8 => "tlbp", 0x18 => "eret", _ => "cop0" };
                    return new(addr, w, m, "", $"{m}();", EndsBlock: m == "eret");
                }
            }
            if (cop == 1)
            {
                if (rs is 0 or 1 or 2 or 4 or 5 or 6)
                {
                    string m = rs switch { 0 => "mfc1", 1 => "dmfc1", 2 => "cfc1", 4 => "mtc1", 5 => "dmtc1", _ => "ctc1" };
                    return rs < 4 ? new(addr, w, m, $"{R[rt]}, {F(rd)}", $"{R[rt]} = {F(rd)};")
                                  : new(addr, w, m, $"{R[rt]}, {F(rd)}", $"{F(rd)} = {R[rt]};");
                }
                if (rs == 8)
                {
                    string m = (rt & 3) switch { 0 => "bc1f", 1 => "bc1t", 2 => "bc1fl", _ => "bc1tl" };
                    uint tgt = Br();
                    string cond = m is "bc1f" or "bc1fl" ? "!fcc" : "fcc";
                    return new(addr, w, m, $"0x{tgt:X8}", $"if ({cond}) goto L_{tgt:X8};", tgt);
                }
                if (Fmt.TryGetValue(rs, out var f))
                {
                    int fs = rd, ft = rt, fd = sa;
                    if (fn >= 0x30)
                        return new(addr, w, $"c.{FCmp[fn & 15]}.{f}", $"{F(fs)}, {F(ft)}", $"fcc = {F(fs)} {FCmp[fn & 15]} {F(ft)};");
                    if (!Cop1Fn.TryGetValue(fn, out var m)) return new(addr, w, ".word", $"0x{w:X8}", "/* cop1 */");
                    if (m is "add" or "sub" or "mul" or "div")
                    {
                        string s = m switch { "add" => "+", "sub" => "-", "mul" => "*", _ => "/" };
                        return new(addr, w, $"{m}.{f}", $"{F(fd)}, {F(fs)}, {F(ft)}", $"{F(fd)} = {F(fs)} {s} {F(ft)};");
                    }
                    return new(addr, w, $"{m}.{f}", $"{F(fd)}, {F(fs)}", $"{F(fd)} = {m.Split('.')[0]}({F(fs)});");
                }
            }
            return new(addr, w, $"cop{cop}", $"0x{w & 0x3FFFFFF:X7}", $"/* cop{cop} op */");
        }
        if (!Ops.TryGetValue(op, out var mn)) return Unk();
        switch (mn)
        {
            case "beq" or "bne" or "beql" or "bnel":
                {
                    uint tgt = Br();
                    if (mn == "beq" && rs == 0 && rt == 0) return new(addr, w, "b", $"0x{tgt:X8}", $"goto L_{tgt:X8};", tgt, false, true);
                    string s = mn.StartsWith("beq") ? "==" : "!=";
                    return new(addr, w, mn, $"{R[rs]}, {R[rt]}, 0x{tgt:X8}", $"if ({R[rs]} {s} {R[rt]}) goto L_{tgt:X8};", tgt);
                }
            case "blez" or "bgtz" or "blezl" or "bgtzl":
                {
                    uint tgt = Br();
                    string s = mn.StartsWith("blez") ? "<=" : ">";
                    return new(addr, w, mn, $"{R[rs]}, 0x{tgt:X8}", $"if ({R[rs]} {s} 0) goto L_{tgt:X8};", tgt);
                }
            case "lui": return new(addr, w, mn, $"{R[rt]}, 0x{imm:X}", $"{R[rt]} = 0x{(uint)imm << 16:X8};");
            case "slti" or "sltiu": return new(addr, w, mn, $"{R[rt]}, {R[rs]}, {Hex(simm)}", $"{R[rt]} = {R[rs]} < {Hex(simm)};");
            case "lwc1" or "ldc1":
                return new(addr, w, mn, $"{F(rt)}, {Hex(simm)}({R[rs]})", $"{F(rt)} = *({(mn == "lwc1" ? "f32" : "f64")}*)({R[rs]} + {Hex(simm)});");
            case "swc1" or "sdc1":
                return new(addr, w, mn, $"{F(rt)}, {Hex(simm)}({R[rs]})", $"*({(mn == "swc1" ? "f32" : "f64")}*)({R[rs]} + {Hex(simm)}) = {F(rt)};");
            case "cache": return new(addr, w, mn, $"0x{rt:X}, {Hex(simm)}({R[rs]})", $"cache_op(0x{rt:X}, {R[rs]} + {Hex(simm)});");
        }
        if (ImmOps.TryGetValue(mn, out var isym))
        {
            int val = mn.Contains("add") ? simm : imm;
            if (mn is "addiu" or "addi" && rs == 0) return new(addr, w, "li", $"{R[rt]}, {Hex(val)}", $"{R[rt]} = {Hex(val)};");
            return new(addr, w, mn, $"{R[rt]}, {R[rs]}, {Hex(val)}", $"{R[rt]} = {R[rs]} {isym} {Hex(val)};");
        }
        string mem = $"{Hex(simm)}({R[rs]})";
        if (LoadT.TryGetValue(mn, out var lt)) return new(addr, w, mn, $"{R[rt]}, {mem}", $"{R[rt]} = *({lt}*)({R[rs]} + {Hex(simm)});");
        if (StoreT.TryGetValue(mn, out var st)) return new(addr, w, mn, $"{R[rt]}, {mem}", $"*({st}*)({R[rs]} + {Hex(simm)}) = {R[rt]};");
        return new(addr, w, mn, "", $"/* {mn} */");
    }

    public static List<Instr> Disassemble(byte[] d, int romOff, uint ram, int count)
    {
        var o = new List<Instr>();
        int end = (int)Math.Min(d.Length, (long)romOff + count * 4L);
        for (int p = romOff; p + 4 <= end; p += 4) o.Add(Decode(BE.U32(d, p), ram + (uint)(p - romOff)));
        return o;
    }

    public static List<uint> FindFunctions(byte[] d, int codeStart, int codeSize, uint ramBase)
    {
        int end = Math.Min(d.Length, codeStart + codeSize);
        var t = new SortedSet<uint> { ramBase };
        uint lo = ramBase, hi = ramBase + (uint)(end - codeStart);
        for (int p = codeStart; p + 4 <= end; p += 4)
        {
            uint w = BE.U32(d, p);
            if ((w >> 26) == 3)
            {
                uint tg = (ramBase & 0xF0000000) | ((w & 0x03FFFFFF) << 2);
                if (tg >= lo && tg < hi) t.Add(tg);
            }
        }
        return t.ToList();
    }

    public static string FunctionToPseudoC(byte[] d, int romOff, uint ram, int maxInstrs = 4000)
    {
        var instrs = new List<Instr>();
        int? endAt = null;
        var all = Disassemble(d, romOff, ram, maxInstrs);
        for (int i = 0; i < all.Count; i++)
        {
            instrs.Add(all[i]);
            if (endAt.HasValue && i >= endAt) break;
            if (all[i].Mnemonic == "jr" && all[i].Operands == "ra") endAt = i + 1;
        }
        var labels = instrs.Where(x => x.Target.HasValue && !x.IsCall).Select(x => x.Target!.Value).ToHashSet();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"void func_{ram:X8}(void) {{");
        foreach (var ins in instrs)
        {
            if (labels.Contains(ins.Addr)) sb.AppendLine($"L_{ins.Addr:X8}:");
            sb.AppendLine($"    {ins.Pseudo,-48} // {ins.Addr:X8}: {ins.Text}");
        }
        sb.Append('}');
        return sb.ToString();
    }

    public static string DisassemblyText(byte[] d, int romOff, uint ram, int count) =>
        string.Join("\n", Disassemble(d, romOff, ram, count).Select(i => $"/* {i.Addr:X8} {i.Word:X8} */  {i.Text}"));
}
