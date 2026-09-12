using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Offline patch: emits no new runtime dependency. Never edits the input executable.
internal static class RobePatcher
{
    static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new Exception("Usage: RobePatcher input.exe output.exe");
            using (var asm = AssemblyDefinition.ReadAssembly(args[0]))
            {
                var module = asm.MainModule;
                var player = module.Types.Single(t => t.FullName == "Terraria.Player");
                var method = player.Methods.Single(m => m.Name == "PackGemStaffFeatures");
                if (player.Methods.Any(m => m.Name == "TaiToolsHasGemRobe"))
                    throw new Exception("Robe patch already installed; no changes made.");
                var item = module.Types.Single(t => t.FullName == "Terraria.Item");
                var armor = player.Fields.Single(f => f.Name == "armor");
                var type = item.Fields.Single(f => f.Name == "type");
                var effective = player.Methods.Single(m => m.Name == "GetEffectiveArmor" && m.Parameters.Count == 1);
                var helper = new MethodDefinition("TaiToolsHasGemRobe", MethodAttributes.Private | MethodAttributes.HideBySig,
                    module.TypeSystem.Boolean);
                helper.Parameters.Add(new ParameterDefinition("expected", ParameterAttributes.None, module.TypeSystem.Int32));
                var il = helper.Body.GetILProcessor();
                var yes = Instruction.Create(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Call, effective);
                il.Emit(OpCodes.Ldfld, type);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Beq, yes);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, armor);
                il.Emit(OpCodes.Ldc_I4, 11);
                il.Emit(OpCodes.Ldelem_Ref);
                il.Emit(OpCodes.Ldfld, type);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Ret);
                il.Append(yes);
                il.Emit(OpCodes.Ret);
                player.Methods.Add(helper);

                var instructions = method.Body.Instructions;
                var patchIl = method.Body.GetILProcessor();
                var count = 0;
                var ids = new[] { 1282, 1283, 1284, 1285, 1286, 1287, 4256 };
                foreach (var constant in instructions.ToArray())
                {
                    if (constant.OpCode != OpCodes.Ldc_I4 || !ids.Contains((int)constant.Operand)) continue;
                    var load = constant.Previous;
                    var compare = constant.Next;
                    if (load.OpCode != OpCodes.Ldloc_0 ||
                        !(compare.OpCode == OpCodes.Ceq || compare.OpCode == OpCodes.Bne_Un ||
                          compare.OpCode == OpCodes.Bne_Un_S || compare.OpCode == OpCodes.Beq || compare.OpCode == OpCodes.Beq_S))
                        throw new Exception("Unrecognized robe comparison IL; refusing this game version.");
                    load.OpCode = OpCodes.Ldarg_0;
                    if (compare.OpCode == OpCodes.Ceq)
                    {
                        compare.OpCode = OpCodes.Call;
                        compare.Operand = helper;
                    }
                    else
                    {
                        bool equality = compare.OpCode == OpCodes.Beq || compare.OpCode == OpCodes.Beq_S;
                        patchIl.InsertBefore(compare, Instruction.Create(OpCodes.Call, helper));
                        compare.OpCode = equality ? OpCodes.Brtrue : OpCodes.Brfalse;
                    }
                    count++;
                }
                if (count != 14) throw new Exception("Expected 14 robe comparisons, found " + count + "; refusing output.");
                // Added calls can push short branches out of range.
                var longCodes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode))
                    .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Name);
                foreach (var instruction in instructions)
                    if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
                        instruction.OpCode = longCodes[instruction.OpCode.Name.Substring(0, instruction.OpCode.Name.Length - 2)];
                asm.Write(args[1]);
                Console.WriteLine("Patched 14 robe checks; formal and social effects combine. Output: " + args[1]);
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
