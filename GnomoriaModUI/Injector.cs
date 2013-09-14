using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Faark.Gnomoria.Modding;
using Faark.Util;

namespace GnomoriaModUI
{
    static class MethodBodyExpands
    {
        private static Instruction CreateSwitchOpCodesByLocal0To4To256(this MethodBody self, VariableDefinition local, OpCode opcode0, OpCode opcode1, OpCode opcode2, OpCode opcode3, OpCode opcodeS, OpCode opcodeAny)
        {
            if ((self.Variables.Count > 0) && (self.Variables[0] == local))
            {
                return Instruction.Create(opcode0);
            }

            if ((self.Variables.Count > 1) && (self.Variables[1] == local))
            {
                return Instruction.Create(opcode1);
            }

            if ((self.Variables.Count > 2) && (self.Variables[2] == local))
            {
                return Instruction.Create(opcode2);
            }

            if ((self.Variables.Count > 3) && (self.Variables[3] == local))
            {
                return Instruction.Create(opcode3);
            }

            if (self.Variables.IndexOf(local) < 256)
            {
                return Instruction.Create(opcodeS, local);
            }

            return Instruction.Create(opcodeAny, local);
        }

        public static Instruction CreateLdloc(this MethodDefinition self, VariableDefinition local)
        {
            return self.Body.CreateSwitchOpCodesByLocal0To4To256(
                local,
                OpCodes.Ldloc_0,
                OpCodes.Ldloc_1,
                OpCodes.Ldloc_2,
                OpCodes.Ldloc_3,
                OpCodes.Ldloc_S,
                OpCodes.Ldloc
                );
        }
    }

    internal class Injector
    {
        protected AssemblyDefinition Assembly { get; private set; }
        protected ModuleDefinition Module { get; private set; }

        protected static class Helper
        {
            private static readonly Random Rand = new Random();
            private static readonly char[] FirstChars = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
            private static readonly char[] AllChars = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            public static string GetRandomName(String end)
            {
                var b = new StringBuilder();

                b.Append(FirstChars[Rand.Next(FirstChars.Length)]);

                for (var i = 0; i < 31; i++)
                {
                    b.Append(AllChars[Rand.Next(AllChars.Length)]);
                }

                b.Append("_").Append(end);

                return b.ToString();
            }

            public static void InjectInstructionsBefore(ILProcessor p, Instruction before, IEnumerable<Instruction> commands)
            {
                var instructions = commands.ToList();

                // Todo: Refactor this.

                /*
                 * following stuff is from http://eatplayhate.wordpress.com/2010/07/18/mono-cecil-vs-obfuscation-fight/
                 * and should redirect jumps?!
                */
                var method = p.Body.Method;
                var oldTarget = before;
                var newTarget = instructions[0];
                var isNewCode = false;

                foreach (var inst in method.Body.Instructions)
                {
                    if( inst == newTarget ){
                        isNewCode = true;
                    }
                    if( inst == before )
                    {
                        isNewCode = false;
                    }

                    if (isNewCode) continue;

                    if ((inst.OpCode.FlowControl == FlowControl.Branch ||
                         inst.OpCode.FlowControl == FlowControl.Cond_Branch) &&
                        inst.Operand == oldTarget)
                        inst.Operand = newTarget;
                }

                foreach (var v in method.Body.ExceptionHandlers)
                {
                    if (v.FilterStart == oldTarget)
                        v.FilterStart = newTarget;
                    if (v.HandlerEnd == oldTarget)
                        v.HandlerEnd = newTarget;
                    if (v.HandlerStart == oldTarget)
                        v.HandlerStart = newTarget;
                    if (v.TryEnd == oldTarget)
                        v.TryEnd = newTarget;
                    if (v.TryStart == oldTarget)
                        v.TryStart = newTarget;
                }

                //update: We now insert after changing, so trgs in the currently inserted code are not changed
                foreach (var instruction in instructions)
                {
                    p.InsertBefore(before, instruction);
                }
            }

            public static void InjectInstructionsBefore(ILProcessor p, Instruction before, params Instruction[] commands)
            {
                InjectInstructionsBefore(p, before, (IEnumerable<Instruction>)commands);
            }

            public static Instruction CreateCallInstruction(ILProcessor ilgen, MethodReference target, bool useVirtIfPossible = true, TypeReference[] genericTypes = null)
            {
                genericTypes = genericTypes ?? new TypeReference[0];

                var callType = OpCodes.Call;

                if (target.HasThis && useVirtIfPossible)
                {
                    callType = OpCodes.Callvirt;
                }

                if (!target.HasGenericParameters) return ilgen.Create(callType, target);

                if (target.GenericParameters.Count != genericTypes.Length)
                {
                    throw new ArgumentException("Invalid generic arguments");
                }

                var genTarget = new GenericInstanceMethod(target);

                for (var i = 0; i < genericTypes.Length; i++)
                {
                    if (target.GenericParameters[i].IsGenericInstance)
                    {
                        throw new NotImplementedException("x");
                    }
                    // Todo: can we validate types here?
                    genTarget.GenericArguments.Add(genericTypes[i]);
                }

                target = genTarget;

                return ilgen.Create(callType, target);
            }
        }

        public Injector(System.IO.FileInfo assemblyFile)
        {
            var assemblyResolver = new DefaultAssemblyResolver();

            assemblyResolver.AddSearchDirectory(assemblyFile.DirectoryName);

            Assembly = AssemblyDefinition.ReadAssembly(
                assemblyFile.FullName,
                new ReaderParameters() { AssemblyResolver = assemblyResolver }
                );

            Module = Assembly.MainModule;
        }

        public void Write(System.IO.FileInfo p)
        {
            Assembly.Write(p.FullName);
        }

        protected TypeDefinition ConvertTypeToTypeDefinition(Type self)
        {
            if (self.Assembly.FullName != Module.Assembly.FullName)
            {
                throw new InvalidOperationException("Cannot convert type to def that is not in the current namespace!");
            }

            var declaringType = (TypeDefinition)Module.LookupToken(self.MetadataToken);
            if (declaringType.FullName != self.FullName)
            {
                throw new ArgumentException("Could not find type [" + self.FullName + "]!");
            }
            return declaringType;
        }

        protected MethodDefinition ConvertMethodBaseToMethodDefinition(System.Reflection.MethodBase method)
        {
            if (method == null) throw new ArgumentNullException("method");

            var declaringType = ConvertTypeToTypeDefinition(method.DeclaringType);
            var methodDefinition = (MethodDefinition)declaringType.Module.LookupToken(method.MetadataToken);
            if (methodDefinition.Name == method.Name) return methodDefinition;

            Debug.Assert(method.DeclaringType != null, "method.DeclaringType != null"); // Todo: Check what this implies...
            throw new ArgumentException("Method [" + method.Name + "] not find type [" + method.DeclaringType.FullName + "]!");
        }

        /// <summary>
        /// Converts a type reference to an actual type.
        /// -> Make this an extension method?
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        protected static Type ConvertTypeReferenceToType(TypeReference self)
        {
            var methodName = self.FullName;

            // Todo: FullName should be wrong for lots of classes (nested eg). Find a better solution, maybe via token, like cecil does?
            if (self.IsGenericInstance)
            {
                var git = self as GenericInstanceType;
                var ungenericType = ConvertTypeReferenceToType( self.GetElementType());
                var genericArgs = new Type[git.GenericArguments.Count];
                for (var i = 0; i < git.GenericArguments.Count; i++)
                {
                    genericArgs[i] = ConvertTypeReferenceToType(git.GenericArguments[i]);
                    // Todo: recursions could be possible?
                }
                return ungenericType.MakeGenericType(genericArgs);
            }

            if (self.IsGenericParameter)
            {
                throw new Exception("Generic params not yet tested, sry. Pls leave me a msg.");
            }

            if (self.IsArray)
            {
                throw new Exception("Arrays not yet tested, sry. Pls leave me a msg.");
            }

            if (self.IsByReference)
            {
                throw new Exception("ByRef not yet tested, sry. Pls leave me a msg.");
            }

            if (self.IsNested)
            {
                //dont think this solution is... "Perfect"
                methodName = methodName.Replace('/', '+');
                //throw new Exception("Nested classes are not yet supported, sry. Pls leave me a msg.");
            }

            /*
             * Token wont work, since we wont get the actual token without using Resolve() first.... :/
            var ass = System.Reflection.Assembly./*ReflectionOnly*Load((self.Scope as AssemblyNameReference).ToString());
            var t = ass.GetModules().Select(mod => mod.ResolveType((int)self.MetadataToken.RID)).Single(el => el != null);
            if (t.Name != self.Name)
            {
                throw new Exception("Failed to resolve type.");
            }*/

            var assembly = self.Scope as AssemblyNameReference;

            if (self.Scope is ModuleDefinition)
            {
                assembly = (self.Scope as ModuleDefinition).Assembly.Name;
            }

            Debug.Assert(assembly != null, "assembly != null");

            var t = Type.GetType(System.Reflection.Assembly.CreateQualifiedName(assembly.FullName, methodName), true);
            /*if( t.MetadataToken != self.MetadataToken.RID ){
                throw new Exception("Failed to resolve type, token does not match.");
            }*/
            return t;
        }

        protected static System.Reflection.MethodBase ConvertMethodReferenceToMethodBase(MethodReference method)
        {
            var type = ConvertTypeReferenceToType(method.DeclaringType);
            var token = method.MetadataToken;

            if (!token.TokenType.HasFlag(TokenType.Method))
            {
                throw new Exception("MethodRef does not look like a method?!");
            }

            var meth = type.Module.ResolveMethod(token.ToInt32());
            if (meth.Name != method.Name) //that should do it for now...
            {
                throw new Exception("Failed to resolve method");
            }

            return meth;
            //misses constructors return type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance| System.Reflection.BindingFlags.Static).Single(el => el.MetadataToken == token.ToInt32());
        }

        protected static void InjectWriteLoadArgHook(ref Instruction[] instructions, int argCnt, ILProcessor ilgen)
        {
            if (argCnt <= 0) return;

            instructions[0] = (ilgen.Create(OpCodes.Ldarg_0));
            if (argCnt <= 1) return;

            instructions[1] = (ilgen.Create(OpCodes.Ldarg_1));

            if (argCnt <= 2) return;
            instructions[2] = (ilgen.Create(OpCodes.Ldarg_2));

            if (argCnt <= 3) return;
            instructions[3] = (ilgen.Create(OpCodes.Ldarg_3));

            if (argCnt <= 4) return;

            for (var i = 4; i < argCnt; i++)
            {
                instructions[i] = (ilgen.Create(OpCodes.Ldarg_S, (byte)i));
            }
        }

        protected static Instruction[] InjectCreateInstructionsHook(ILProcessor ilgen, int argCnt, MethodReference mref, TypeReference[] genericCallArgs)
        {
            var list = new Instruction[argCnt + 1];

            InjectWriteLoadArgHook(ref list, argCnt, ilgen);

            list[argCnt] = Helper.CreateCallInstruction(ilgen, mref, false, genericCallArgs);

            return list;
        }

        private readonly Dictionary<MethodDefinition, VariableDefinition> _localVarsUsedToCacheStoreOutResults = new Dictionary<MethodDefinition, VariableDefinition>();
        protected class HookInjector
        {
            public MethodDefinition OriginalMethod { get; protected set; }
            public MethodReference CustomMethodReference { get; protected set; }
            public MethodHookType HookType { get; protected set; }
            public MethodHookFlags HookFlags { get; protected set; }
            public Injector Injector { get; private set; }

            protected readonly TypeReference[] GenericArguments;

            public ILProcessor ILGen { get; protected set; }

            public HookInjector(Injector injector, MethodDefinition originalMethod, MethodReference customMethodReference, MethodHookType hookType, MethodHookFlags hookFlags)
            {
                Injector = injector;
                OriginalMethod = originalMethod;
                CustomMethodReference = customMethodReference;
                HookType = hookType;
                HookFlags = hookFlags;
                GenericArguments = new TypeReference[originalMethod.GenericParameters.Count];
                if (originalMethod.HasGenericParameters)
                {
                    //throw new NotImplementedException("Hooking generic instances? Never tested it yet. Contact creator, pls!");
                    for (var i = 0; i < originalMethod.GenericParameters.Count; i++)
                    {
                        GenericArguments[i] = originalMethod.GenericParameters[i];
                    }
                }
                originalMethod.Body.SimplifyMacros();
                ILGen = OriginalMethod.Body.GetILProcessor();
            }

            protected Instruction CurrentTargetInstruction;

            protected virtual IEnumerable<Instruction> CreateInstructions_PreHook()
            {
                yield break;
            }

            protected VariableDefinition localVarUsedToChacheOutResult()
            {
                VariableDefinition var;
                if (!Injector._localVarsUsedToCacheStoreOutResults.TryGetValue(OriginalMethod, out var))
                {
                    OriginalMethod.Body.Variables.Add(Injector._localVarsUsedToCacheStoreOutResults[OriginalMethod] = var = new VariableDefinition("temp_ret_val_out_cache", OriginalMethod.ReturnType));
                }
                return var;
            }

            protected virtual IEnumerable<Instruction> CreateInstructions_Hook_LoadArgs()
            {
                var argCnt = OriginalMethod.Parameters.Count + (OriginalMethod.IsStatic ? 0 : 1);

                if (argCnt > 0)
                {
                    yield return (ILGen.Create(OpCodes.Ldarg_0));
                    if (argCnt > 1)
                    {
                        yield return (ILGen.Create(OpCodes.Ldarg_1));
                        if (argCnt > 2)
                        {
                            yield return (ILGen.Create(OpCodes.Ldarg_2));
                            if (argCnt > 3)
                            {
                                yield return (ILGen.Create(OpCodes.Ldarg_3));
                                if (argCnt > 4)
                                {
                                    for (var i = 4; i < argCnt; i++)
                                    {
                                        yield return (ILGen.Create(OpCodes.Ldarg_S, (byte)i));
                                    }
                                }
                            }
                        }
                    }
                }

                if (HookFlags.HasFlag(MethodHookFlags.CanSkipOriginal) && (ConvertTypeReferenceToType(OriginalMethod.ReturnType) != typeof(void)))
                {
                    yield return ILGen.Create(OpCodes.Ldloca_S, localVarUsedToChacheOutResult());
                }
            }

            protected virtual IEnumerable<Instruction> CreateInstructions_Hook_Call()
            {
                yield return Helper.CreateCallInstruction(ILGen, CustomMethodReference, false, GenericArguments);
            }

            protected virtual IEnumerable<Instruction> CreateInstructions_Hook()
            {
                return CreateInstructions_Hook_LoadArgs().Union(CreateInstructions_Hook_Call());
            }

            protected virtual IEnumerable<Instruction> CreateInstructions_PostHook()
            {
                if (!HookFlags.HasFlag(MethodHookFlags.CanSkipOriginal))
                    yield break;

                yield return ILGen.Create(OpCodes.Brfalse_S, CurrentTargetInstruction);
                if (ConvertTypeReferenceToType(OriginalMethod.ReturnType) != typeof(void))
                {
                    yield return OriginalMethod.CreateLdloc(localVarUsedToChacheOutResult());
                }
                yield return ILGen.Create(OpCodes.Ret);
            }

            protected virtual IEnumerable<Instruction> CreateHookInstructions()
            {
                return CreateInstructions_PreHook().Union(CreateInstructions_Hook()).Union(CreateInstructions_PostHook());
            }

            public void Inject()
            {
                switch (HookType)
                {
                    case MethodHookType.RunBefore:
                        CurrentTargetInstruction = OriginalMethod.Body.Instructions[0];
                        Helper.InjectInstructionsBefore(
                            ILGen,
                            CurrentTargetInstruction,
                            CreateHookInstructions()
                            );
                        break;
                    case MethodHookType.RunAfter:
                        //scan for all RET's and insert our call before it...
                        for (var i = 0; i < OriginalMethod.Body.Instructions.Count; i++)
                        {
                            if (OriginalMethod.Body.Instructions[i].OpCode == OpCodes.Ret)
                            {
                                CurrentTargetInstruction = OriginalMethod.Body.Instructions[i];
                                var newInstructions = CreateHookInstructions();
                                Helper.InjectInstructionsBefore(
                                    ILGen,
                                    CurrentTargetInstruction,
                                    newInstructions
                                    );
                                i += newInstructions.Count();
                            }
                        }
                        break;
                    case MethodHookType.Replace:
                        OriginalMethod.Body.Instructions.Clear();
                        OriginalMethod.Body.ExceptionHandlers.Clear();
                        OriginalMethod.Body.Variables.Clear();
                        CurrentTargetInstruction = ILGen.Create(OpCodes.Ret);
                        OriginalMethod.Body.Instructions.Add(CurrentTargetInstruction);
                        Helper.InjectInstructionsBefore(ILGen, CurrentTargetInstruction, CreateHookInstructions());
                        break;
                    default:
                        throw new NotImplementedException("Only Before and After & replace are implemented, yet");
                }
                OriginalMethod.Body.OptimizeMacros();
            }
        }

        protected class CustomLoadArgsHookInjector: HookInjector
        {
            private readonly List<Tuple<OpCode, byte?>> _instructionData;

            public CustomLoadArgsHookInjector(Injector inj, List<Tuple<OpCode, byte?>> instructionData, MethodDefinition methodBase, MethodReference methodInfo, MethodHookType methodHookType, MethodHookFlags methodHookFlags)
                : base(inj, methodBase, methodInfo, methodHookType, methodHookFlags)
            {
                _instructionData = instructionData;
            }

            protected override IEnumerable<Instruction> CreateInstructions_Hook_LoadArgs()
            {
                return _instructionData
                    .Select(instr => instr.Item2 == null ? ILGen.Create(instr.Item1) : ILGen.Create(instr.Item1, instr.Item2.Value));
//#warning return instructionData.Select(instr => instr.Item2 == null ? ILGen.Create(instr.Item1) : ILGen.Create(instr.Item1, instr.Item2.Value)).Union(new Instruction[] { Helper.CreateCallInstruction(ILGen, CustomMethodReference, false, GenericArguments) });
//                throw new NotImplementedException();
            }
        }


        /*protected void InjectHook(
            MethodDefinition originalMethod,
            MethodReference customMethod_reference,
            int arguments_to_load_count,
            MethodHookType hookType,
            MethodHookFlags hookFlags
            )
        {
        }*/

        protected void InjectHook(
          MethodDefinition originalMethod,
          MethodReference customMethodReference,
          MethodHookType hookType,
          MethodHookFlags hookFlags
          )
        {
            var hooker = new HookInjector(this, originalMethod, customMethodReference, hookType, hookFlags);
            hooker.Inject();
        }

        protected void InjectHook(MethodHook hook)
        {
            InjectHook(
                ConvertMethodBaseToMethodDefinition(hook.InterceptedMethod),
                Module.Import(hook.CustomMethod),
                hook.HookType,
                hook.HookFlags
                );
        }

        protected void InjectVirtual(MethodAddVirtual methodAddVirtual)
        {
            var lookedUpTargetFunc = Module.Import(methodAddVirtual.InterceptedMethod);
            var lookedUpFunc = Module.Import(methodAddVirtual.CustomMethod);
            var trgType = ConvertTypeToTypeDefinition(methodAddVirtual.ModifyingType);
            var retType = (methodAddVirtual.InterceptedMethod as System.Reflection.MethodInfo).ReturnType;
            var lookedUpRetType = Module.Import(retType.IsGenericParameter ? typeof(void) : retType);

            var newMethod = new MethodDefinition(
                methodAddVirtual.InterceptedMethod.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                lookedUpRetType
                );
            trgType.Methods.Add(newMethod);

            var genArguments = new TypeReference[lookedUpTargetFunc.GenericParameters.Count];
            for (var i = 0; i < lookedUpTargetFunc.GenericParameters.Count; i++)
            {
                var genpa = lookedUpTargetFunc.GenericParameters[i];
                if (genpa.DeclaringMethod != lookedUpTargetFunc)
                {
                    throw new NotImplementedException("Generic arguments in functions that are not declared by func not yet implemented. Pls contact the author!");
                }

                var newGen = new GenericParameter(newMethod) {Name = genpa.Name};
                newMethod.GenericParameters.Add(newGen);

                if (lookedUpTargetFunc.ReturnType == genpa)
                {
                    newMethod.ReturnType = newGen;
                }

                genArguments[i] = newGen;
            }

            foreach (var param in methodAddVirtual.InterceptedMethod.GetParameters())
            {
                var newParam = new ParameterDefinition(null, ParameterAttributes.None, Module.Import(param.ParameterType));
                if (param.ParameterType.IsGenericParameter)
                {
                    throw new NotImplementedException("generic params not yet tested! pls contact author");
                }
                if (param.ParameterType.IsGenericType)
                {
                    throw new NotImplementedException("generic params not yet tested! pls contact author");
                }
                if (param.IsIn)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.In;
                }
                if (param.IsLcid)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                if (param.IsOut)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                if (param.IsRetval)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                if (param.IsOptional)
                {
                    throw new NotImplementedException("special params not yet tested! pls contact author");
                    //new_param.Attributes = new_param.Attributes & ParameterAttributes.;
                }
                newMethod.Parameters.Add(newParam);
            }

            var argCount = methodAddVirtual.GetRequiredParameterLayout().Count();
            var ilgen = newMethod.Body.GetILProcessor();

            if (methodAddVirtual.HookType == MethodHookType.Replace)
            {
                var callCmds = InjectCreateInstructionsHook(ilgen, argCount, lookedUpFunc, genArguments);
                foreach (var i in callCmds)
                {
                    newMethod.Body.Instructions.Add(i);
                }
                newMethod.Body.Instructions.Add(ilgen.Create(OpCodes.Ret));
            }
            else
            {
                if (methodAddVirtual.HasResultAsFirstParameter)
                {
                    argCount--;
                }
                var instr = new Instruction[argCount + 2];
                InjectWriteLoadArgHook(ref instr, argCount, ilgen);

                instr[argCount] = Helper.CreateCallInstruction(ilgen, lookedUpTargetFunc, false, genArguments);
                instr[argCount + 1] = ilgen.Create(OpCodes.Ret);
                foreach (var i in instr)
                {
                    newMethod.Body.Instructions.Add(i);
                }
                InjectHook(
                    newMethod,
                    lookedUpFunc,
                    methodAddVirtual.HookType,
                    methodAddVirtual.HookFlags
                    );
            }
        }

        protected void Inject_RefHook(MethodRefHook methodRefHook)
        {
            var instructionData = new List<Tuple<OpCode, byte?>>();
            var requredParameterLayout = methodRefHook.GetRequiredParameterLayout().ToList();
            var foundParameterLayout = methodRefHook.InterceptedMethod.GetParameters();
            var customArgCount = methodRefHook.InterceptedMethod.IsStatic ? 0 : 1;

            for (var i = 0; (i < requredParameterLayout.Count); i++)
            {
                if ((i >= customArgCount) && requredParameterLayout[i].ParameterType.IsByRef && !foundParameterLayout[i - customArgCount].ParameterType.IsByRef)
                {
                    instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarga_S, (byte)i));
                }
                else switch (i)
                {
                    case 0:
                        instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_0, null));
                        break;
                    case 1:
                        instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_1, null));
                        break;
                    case 2:
                        instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_2, null));
                        break;
                    case 3:
                        instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_3, null));
                        break;
                    default:
                        instructionData.Add(new Tuple<OpCode, byte?>(OpCodes.Ldarg_S, (byte)i));
                        break;
                }
            }

            var hooker = new CustomLoadArgsHookInjector(
                this,
                instructionData,
                ConvertMethodBaseToMethodDefinition(methodRefHook.InterceptedMethod),
                Module.Import(methodRefHook.CustomMethod),
                methodRefHook.HookType,
                methodRefHook.HookFlags
                );
            hooker.Inject();
        }

        protected void Inject_AddEnumElement(EnumAddElement enumAddElement)
        {
            var enumType = ConvertTypeToTypeDefinition(enumAddElement.EnumToChange);
            if (enumType.Fields.Count(field => field.Name.ToUpper() == enumAddElement.NewEnumName.ToUpper()) > 0)
            {
                throw new InvalidOperationException("Enum [" + enumType.FullName + "] does already contain a field named [" + enumAddElement.NewEnumName + "]!");
            }

            var newField = new FieldDefinition(enumAddElement.NewEnumName, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.Family | FieldAttributes.HasDefault, enumType);
            if (enumAddElement.NewEnumValue == null)
            {
                newField.Constant = enumType.Fields.Where(field => field.HasConstant).Max(field => (int)field.Constant) + 1;
            }
            else
            {
                newField.Constant = enumAddElement.NewEnumValue;
            }
            enumType.Fields.Add(newField);
        }

        protected MethodReference Inject_ClassChangeBase_GetSimilarInstanceMethod(MethodReference method, TypeReference type)
        {
            var refMeth = ConvertMethodReferenceToMethodBase(method);
            if (refMeth.IsStatic)
            {
                return null;
            }
            var t = ConvertTypeReferenceToType(type);
            var r = t
                .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Cast<System.Reflection.MethodBase>()
                .Union(t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                .Where(meth => meth.DeclaringType == t)
                .Where(meth => meth.Name == method.Name)
                .Where(meth => meth.GetParameters().SequenceEqual(refMeth.GetParameters(), (a, b) => ((CustomParameterInfo)a).IsSimilar(b)))
                .Select(meth => Module.Import(meth))
                .SingleOrDefault();
            return r;
        }

        protected void Inject_ClassChangeBase(ClassChangeBase classChangeBase)
        {
            var trgClass = ConvertTypeToTypeDefinition(classChangeBase.ClassToChange);
            var newBase = Module.Import(classChangeBase.NewBaseClass);
            var oldBase = trgClass.BaseType;
            trgClass.BaseType = newBase;
            foreach (var method in trgClass.Methods)
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if ((instruction.OpCode == OpCodes.Call) || (instruction.OpCode == OpCodes.Callvirt))
                    {
                        var trg = instruction.Operand as MethodReference;
                        Debug.Assert(trg != null, "trg != null");

                        if (oldBase == trg.DeclaringType)
                        {
                            instruction.Operand = Inject_ClassChangeBase_GetSimilarInstanceMethod(trg, newBase) ?? trg;
                        }
                    }
                    else if (instruction.OpCode == OpCodes.Calli)
                    {
                        throw new Exception("Your trg class contains a calli command. Please leave me a msg, since i never saw an use case, yet.");
                    }
                }
            }
        }


        protected void Inject_ClassCreationHook(ClassCreationHook classCreationHook)
        {
            throw new NotImplementedException("ClassCreationHook is not usable atm.");

#if false
            var meth = ConvertMethodBaseToMethodDefinition(classCreationHook.InterceptCreationInMethod);
            //var conType = Module.Import(classCreationHook.ClassToInterceptCreation);
            var ilgen = meth.Body.GetILProcessor();
            for (var i = 0; i < meth.Body.Instructions.Count; i++)
            {
                var ins = meth.Body.Instructions[i];
                if (ins.OpCode == OpCodes.Newobj)
                {
                    var trgMeth = ins.Operand as MethodReference;
                    if( ConvertTypeReferenceToType(trgMeth.DeclaringType) == classCreationHook.ClassToInterceptCreation)
                    //if ((trgMeth.DeclaringType == conType) && trgMeth.Name == ".ctor")
                    {
                        meth.Body.Instructions[i] = ilgen.Create(OpCodes.Call, Module.Import(classCreationHook.CustomCreationMethod));
                    }
                }
            }
            //throw new NotImplementedException();
#endif
        }

        public void Inject_Modification(IModification modification)
        {
            if (modification == null)
            {
                throw new Exception("Modification is null."); // TODO: Use ArgumentNullException
            }

            if (modification is MethodHook)
            {
                InjectHook(modification as MethodHook);
            }
            else if (modification is MethodAddVirtual)
            {
                InjectVirtual(modification as MethodAddVirtual);
            }
            else if (modification is MethodRefHook)
            {
                Inject_RefHook(modification as MethodRefHook);
            }
            else if (modification is EnumAddElement)
            {
                Inject_AddEnumElement(modification as EnumAddElement);
            }
            else if (modification is ClassChangeBase)
            {
                Inject_ClassChangeBase(modification as ClassChangeBase);
            }
#if false
            else if (modification is ClassCreationHook)
            {
                Inject_ClassCreationHook(modification as ClassCreationHook);
            }
#endif
            else if (modification is IModificationCollection)
            {
                foreach (var subMod in (modification as IModificationCollection))
                {
                    Inject_Modification(subMod);
                }
            }
            else
            {

                throw new Exception("Unknown change [" + modification.GetType().FullName + "]; failed to apply!");
            }
        }


        public bool AssemblyContainsType(Type type)
        {
            return type.Assembly.FullName == Module.Assembly.FullName;
        }
    }

    class GnomoriaExeInjector : Injector
    {
        public GnomoriaExeInjector(System.IO.FileInfo gnomoriaExe) : base(gnomoriaExe) { }

        public void Inject_CallTo_ModRuntimeController_Initialize_AtStartOfMain(System.IO.FileInfo modControllerFile)
        {
            /*
             * first we need to load the assembly.
             * Then we can call a function that contains a ref to our DLL.
             * This serparate func is not allowed to IL before we load, so it can't be in the same func
             */

            // part1: create the new func that calls our module stuff
            var ep = Assembly.EntryPoint;
            var methodThatCallsOurModule = new MethodDefinition(
                Helper.GetRandomName("ModRuntimeController_Initialize"),
                MethodAttributes.HideBySig | MethodAttributes.Static,
                Module.Import(typeof(void))
                );
            methodThatCallsOurModule.Parameters.Add(new ParameterDefinition("args", ParameterAttributes.None, Module.Import(typeof(string[]))));
            var methodThatCallsBody = methodThatCallsOurModule.Body.GetILProcessor();
            //CODE FOR: Faark.Gnomoria.Modding.ModRuntimeController.Initiallize();
            methodThatCallsBody.Append(methodThatCallsBody.Create(OpCodes.Ldarg_0));
            methodThatCallsBody.Append(methodThatCallsBody.Create(OpCodes.Call, Module.Import(Method.Of<string[]>(RuntimeModController.Initialize))));
            methodThatCallsBody.Append(methodThatCallsBody.Create(OpCodes.Ret));
            ep.DeclaringType.Methods.Add(methodThatCallsOurModule);

            // part2: inject code into games EP to load our assembly and call the just created func
            var commands = new List<Instruction>();
            var entryPointProcessor = ep.Body.GetILProcessor();
            Instruction skipLoadBranch = null;

            if (modControllerFile != null)
            {
                var linqContainsString = new GenericInstanceMethod(Module.Import(typeof(Enumerable).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Single(mi => mi.Name == "Contains" && mi.GetParameters().Length == 2)));
                linqContainsString.GenericArguments.Add(Module.Import(typeof(string)));
                //CODE FOR:
                //System.Reflection.Assembly.LoadFrom("C:\\Dokumente und Einstellungen\\Administrator\\Eigene Dateien\\Visual Studio 2010\\Projects\\GnomModTechDemo\\bin\\Release\\ModController.dll");
                commands.Add(entryPointProcessor.Create(OpCodes.Ldarg_0));
                commands.Add(entryPointProcessor.Create(OpCodes.Ldstr, "-noassemblyloading"));
                commands.Add(entryPointProcessor.Create(OpCodes.Call, linqContainsString));
                commands.Add(skipLoadBranch = entryPointProcessor.Create(OpCodes.Brtrue_S, entryPointProcessor.Body.Instructions[0]));
                commands.Add(entryPointProcessor.Create(OpCodes.Ldstr, modControllerFile.FullName));
                commands.Add(entryPointProcessor.Create(OpCodes.Call, Module.Import(Method.Of<String, System.Reflection.Assembly>(System.Reflection.Assembly.LoadFrom))));
                commands.Add(entryPointProcessor.Create(OpCodes.Pop));
            }

            var loadArgs = entryPointProcessor.Create(OpCodes.Ldarg_0);
            commands.Add(loadArgs);

            var callOurMethodInstruction = Helper.CreateCallInstruction(entryPointProcessor, methodThatCallsOurModule, false);
            commands.Add(callOurMethodInstruction);

            if (skipLoadBranch != null)
            {
                skipLoadBranch.Operand = loadArgs;
            }

            Helper.InjectInstructionsBefore(entryPointProcessor, ep.Body.Instructions[0], commands);
        }

        /*
         * Assembly resolving will now be handled by the launcher. No more need to do this via IL manipulation :)
         *
        public void Inject_CurrentAppDomain_AddResolveEventAtStartOfMain()
        {
            /*
             * adds a eventlistener to AppDomain.ResolveEvent
             *
             * Part1: Create the event func:
                        static System.Reflection.Assembly CurrentDomain_AssemblyResolveClassic(object sender, ResolveEventArgs args)
                        {
                            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                if (a.FullName == args.Name)
                                    return a;
                            }
                            return null;
                        }
            *

            var resolveEventMethod = new MethodDefinition(Helper.GetRandomName("CurrentAppDomain_AssemblyResolve"),
                MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.Private,
                Module.Import(typeof(System.Reflection.Assembly))
                );
            resolveEventMethod.Parameters.Add(new ParameterDefinition("sender", ParameterAttributes.None, Module.Import(typeof(object))));
            resolveEventMethod.Parameters.Add(new ParameterDefinition("args", ParameterAttributes.None, Module.Import(typeof(System.ResolveEventArgs))));
            var local1_assembly = new VariableDefinition(Module.Import(typeof(System.Reflection.Assembly)));
            var local2_assembly = new VariableDefinition(Module.Import(typeof(System.Reflection.Assembly)));
            var local3_assemblies = new VariableDefinition(Module.Import(typeof(System.Reflection.Assembly[])));
            var local4_int = new VariableDefinition(Module.Import(typeof(int)));

            resolveEventMethod.Body.Variables.Add(local1_assembly);
            resolveEventMethod.Body.Variables.Add(local2_assembly);
            resolveEventMethod.Body.Variables.Add(local3_assemblies);
            resolveEventMethod.Body.Variables.Add(local4_int);
            resolveEventMethod.Body.InitLocals = true;
            var resMethIL = resolveEventMethod.Body.GetILProcessor();

            var trgDummy = resMethIL.Create(OpCodes.Nop);
            var srcList = new Instruction[4];
            var trgList = new Instruction[4];
            var ils = new Instruction[]{
                resMethIL.Create(OpCodes.Call, Module.Import(typeof(System.AppDomain).GetProperty("CurrentDomain").GetGetMethod())),
                resMethIL.Create(OpCodes.Callvirt, Module.Import(typeof(System.AppDomain).GetMethod("GetAssemblies", new Type[]{}))),
                resMethIL.Create(OpCodes.Stloc_2),
                resMethIL.Create(OpCodes.Ldc_I4_0),
                resMethIL.Create(OpCodes.Stloc_3),
   srcList[0] = resMethIL.Create(OpCodes.Br_S, trgDummy),
   trgList[3] = resMethIL.Create(OpCodes.Ldloc_2),
                resMethIL.Create(OpCodes.Ldloc_3),
                resMethIL.Create(OpCodes.Ldelem_Ref),
                resMethIL.Create(OpCodes.Stloc_0),
                resMethIL.Create(OpCodes.Ldloc_0),
                resMethIL.Create(OpCodes.Callvirt, Module.Import(typeof(System.Reflection.Assembly).GetProperty("FullName").GetGetMethod())),
                resMethIL.Create(OpCodes.Ldarg_1),
                resMethIL.Create(OpCodes.Callvirt, Module.Import(typeof(System.ResolveEventArgs).GetProperty("Name").GetGetMethod())),
                resMethIL.Create(OpCodes.Call, Module.Import(typeof(System.String).GetMethod("op_Equality"))),
   srcList[1] = resMethIL.Create(OpCodes.Brfalse_S, trgDummy),
                resMethIL.Create(OpCodes.Ldloc_0),
                resMethIL.Create(OpCodes.Stloc_1),
   srcList[2] = resMethIL.Create(OpCodes.Leave_S, trgDummy),
   trgList[1] = resMethIL.Create(OpCodes.Ldloc_3),
                resMethIL.Create(OpCodes.Ldc_I4_1),
                resMethIL.Create(OpCodes.Add),
                resMethIL.Create(OpCodes.Stloc_3),
   trgList[0] = resMethIL.Create(OpCodes.Ldloc_3),
                resMethIL.Create(OpCodes.Ldloc_2),
                resMethIL.Create(OpCodes.Ldlen),
                resMethIL.Create(OpCodes.Conv_I4),
   srcList[3] = resMethIL.Create(OpCodes.Blt_S, trgDummy),
                resMethIL.Create(OpCodes.Ldnull),
                resMethIL.Create(OpCodes.Ret),
   trgList[2] = resMethIL.Create(OpCodes.Ldloc_1),
                resMethIL.Create(OpCodes.Ret)
            };
            for (var i = 0; i < srcList.Length; i++)
            {
                srcList[i].Operand = trgList[i];
            }
            foreach (var i in ils)
            {
                resMethIL.Append(i);
            }

            var ep = Assembly.EntryPoint;
            ep.DeclaringType.Methods.Add(resolveEventMethod);

            var adil = ep.Body.GetILProcessor();
            //Part2: bind the event. AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            var linqContainsString = new GenericInstanceMethod(Module.Import(typeof(System.Linq.Enumerable).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).Single(mi => mi.Name == "Contains" && mi.GetParameters().Length == 2)));
            linqContainsString.GenericArguments.Add(Module.Import(typeof(string)));
            Instruction ifFalseGoto, oldFirstInstruction = ep.Body.Instructions[0];
            var adils = new Instruction[]{
                //adil.Create(OpCodes.Ldarg_0),
                //adil.Create(OpCodes.Call, module.Import(Method.Of<IEnumerable<object>>(RuntimeModController.WriteLogO))),
                adil.Create(OpCodes.Ldarg_0),
                adil.Create(OpCodes.Ldstr, "-noassemblyresolve"),
                adil.Create(OpCodes.Call, linqContainsString),
  ifFalseGoto = adil.Create(OpCodes.Brtrue_S, ep.Body.Instructions[0]),
                //adil.Create(OpCodes.Ret),
                adil.Create(OpCodes.Call, Module.Import(typeof(System.AppDomain).GetProperty("CurrentDomain").GetGetMethod())),
                adil.Create(OpCodes.Ldnull),
                adil.Create(OpCodes.Ldftn, resolveEventMethod),
                adil.Create(OpCodes.Newobj, Module.Import(typeof(System.ResolveEventHandler).GetConstructor(new Type[]{ typeof(object), typeof( IntPtr)}))),
                adil.Create(OpCodes.Callvirt, Module.Import(typeof(System.AppDomain).GetEvent("AssemblyResolve").GetAddMethod()))
            };
            Helper.InjectInstructionsBefore(adil, oldFirstInstruction, adils);
            ifFalseGoto.Operand = oldFirstInstruction;
        }
        */

        private void Inject_TryCatchWrapperAroundEverything(MethodDefinition methodToWrap, Func<ILProcessor, VariableDefinition, Instruction[]> getIlCallback, Type exceptionType = null)
        {
            if (exceptionType == null)
            {
                exceptionType = typeof(Exception);
            }
            var il = methodToWrap.Body.GetILProcessor();
            var exVar = new VariableDefinition(Module.Import(exceptionType));
            methodToWrap.Body.Variables.Add(exVar);
            var handlerCode = new List<Instruction>();
            handlerCode.Add(il.Create(OpCodes.Stloc, exVar));
            handlerCode.AddRange(getIlCallback(il, exVar));
            var ret = il.Create(OpCodes.Ret);
            var leave = il.Create(OpCodes.Leave, ret);
            //var leave = il.Create(OpCodes.Rethrow);

            methodToWrap.Body.Instructions.Last().OpCode = OpCodes.Leave;
            methodToWrap.Body.Instructions.Last().Operand = ret;

            il.InsertAfter(
                methodToWrap.Body.Instructions.Last(),
                leave);
            il.InsertAfter(leave, ret);

            Helper.InjectInstructionsBefore(il, leave, handlerCode);


            var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = methodToWrap.Body.Instructions.First(),
                TryEnd = handlerCode[0],
                HandlerStart = handlerCode[0],
                HandlerEnd = ret,
                CatchType = Module.Import(typeof(Exception)),
            };

            methodToWrap.Body.ExceptionHandlers.Add(handler);
        }

        /*
         * Got it by the launcher, now
        public void Inject_TryCatchWrapperAroundEverthingInMain_WriteCrashLog()
        {
            Inject_TryCatchWrapperAroundEverything(
                Assembly.EntryPoint,
                (il, exVar) =>
                {
#warning implement a better error handler instead of fkng msgbox
                    return new Instruction[]{
                        //File.WriteAllText(Path.GetTempFileName(), err.ToString());
                        il.Create(OpCodes.Call, Module.Import(Method.Of<string>(System.IO.Path.GetTempFileName))),
                        il.Create(OpCodes.Ldloc, exVar),
                        il.Create(OpCodes.Callvirt, Module.Import(typeof(System.Object).GetMethod("ToString", new Type[] { }))),
                        il.Create(OpCodes.Call, Module.Import(Method.Of<string, string>(System.IO.File.WriteAllText))),

                        //MessageBox.Show(err.ToString());
                        il.Create(OpCodes.Ldloc, exVar),
                        il.Create(OpCodes.Callvirt, Module.Import(typeof(System.Object).GetMethod("ToString", new Type[] { }))),
                        il.Create(OpCodes.Call, Module.Import(typeof(System.Windows.Forms.MessageBox).GetMethod("Show", new Type[] { typeof(string) }))),
                        il.Create(OpCodes.Pop)
                    };
                });

            //http://stackoverflow.com/questions/11074518/add-a-try-catch-with-mono-cecil
            /* this cant run, since it isn't referenced while compiling EntryPoint. Also it does not make sense to wrapp LoadAssembly(Mod.dll) with it in case that fails...
             * var write = il.Create(
                OpCodes.Call,
                module.Import(typeof(Faark.Gnomoria.Modding.ModRuntimeController).GetMethod("WriteCrashLog")));** /
            //var write1 = il.Create(OpCodes.Callvirt, module.Import(typeof(System.Object).GetMethod("ToString", new Type[] { })));
            //var write2 = il.Create(OpCodes.Call, module.Import(typeof(System.Windows.Forms.MessageBox).GetMethod("Show", new Type[] { typeof(string) })));
            //var write3 = il.Create(OpCodes.Pop);
        }*/
        /*
         * Launchers firstchanceexception should catch this, now
        public void Inject_TryCatchWrapperAroundGnomanEmpire_LoadGame()
        {
            Inject_TryCatchWrapperAroundEverything(
                Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "LoadGame"),
                (il, exVar) =>
                {
                    return new Instruction[]{
                        il.Create( OpCodes.Ldloc, exVar),
                        il.Create( OpCodes.Call, Module.Import(Method.Of<Exception>(RuntimeModController.WriteLog))),
                        il.Create( OpCodes.Rethrow )
                    };
                }
            );
        }*/

        public void Inject_AddHighDefXnaProfile()
        {
            Module.Resources.Add(new EmbeddedResource("Microsoft.Xna.Framework.RuntimeProfile", ManifestResourceAttributes.Public, Encoding.ASCII.GetBytes("Windows.v4.0.HiDef\n")));
            //Module.Resources.Add(new EmbeddedResource("Microsoft.Xna.Framework.RuntimeProfile", ManifestResourceAttributes.Public, Encoding.ASCII.GetBytes("Windows.v4.0.Reach\n")));
        }

        public void Inject_SetContentRootDirectoryToCurrentDir_InsertAtStartOfMain()
        {
            // TODO: Find out why this is needed.
            var meth = Assembly.EntryPoint;
            var il = meth.Body.GetILProcessor();

            var getGnome = Module.GetType("Game.GnomanEmpire").Properties.Single(prop => prop.Name == "Instance").GetMethod;
            var getCmgr = ConvertTypeReferenceToType(Module.GetType("Game.GnomanEmpire").BaseType).GetProperties().Single(prop => prop.Name == "Content").GetGetMethod();
            var getPath = Module.Import(Method.Of<string>(System.IO.Directory.GetCurrentDirectory));
            var setRoot = getCmgr.ReturnType.GetProperties().Single(prop => prop.Name == "RootDirectory").GetSetMethod();
            var cmds = new[]{
                il.Create(OpCodes.Call, getGnome),
                il.Create(OpCodes.Callvirt, Module.Import(getCmgr)),
                il.Create(OpCodes.Call,  Module.Import(getPath)),
                il.Create(OpCodes.Ldstr, "Content"),
                il.Create(OpCodes.Call, Module.Import(Method.Func<string, string, string>(System.IO.Path.Combine))),
                il.Create(OpCodes.Callvirt,  Module.Import(setRoot))
            };

            Helper.InjectInstructionsBefore(il, meth.Body.Instructions[0], cmds);
        }

        public void Inject_SaveLoadCalls()
        {
            InjectHook(
                Module.GetType("Game.Map").Methods.Single(m => m.Name == "GenerateMap"),
                Module.Import(typeof(RuntimeModController).GetMethod("PreCreateHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunBefore,
                MethodHookFlags.None);
            InjectHook(
                Module.GetType("Game.Map").Methods.Single(m => m.Name == "GenerateMap"),
                Module.Import(typeof(RuntimeModController).GetMethod("PostCreateHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunAfter,
                MethodHookFlags.None);
            InjectHook(
                Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "LoadGame"),
                Module.Import(typeof(RuntimeModController).GetMethod("PreLoadHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunBefore,
                MethodHookFlags.None);
            InjectHook(
                Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "LoadGame"),
                Module.Import(typeof(RuntimeModController).GetMethod("PostLoadHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                MethodHookType.RunAfter,
                MethodHookFlags.None);
            InjectHook(
                 Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "SaveGame"),
                 Module.Import(typeof(RuntimeModController).GetMethod("PreSaveHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                 MethodHookType.RunBefore,
                 MethodHookFlags.None);
            InjectHook(
                 Module.GetType("Game.GnomanEmpire").Methods.Single(m => m.Name == "SaveGame"),
                 Module.Import(typeof(RuntimeModController).GetMethod("PostSaveHook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)),
                 MethodHookType.RunAfter,
                 MethodHookFlags.None);
        }

        public void Debug_RemoveExceptionHandler(ExceptionHandler eh, MethodBody mb)
        {
            // Todo: Find out what this does.
            var catchStart = mb.Instructions.IndexOf(eh.HandlerStart) - 1;
            var catchEnd = mb.Instructions.IndexOf(eh.HandlerEnd);
            for (var i = catchEnd - 1; i >= catchStart; i--)
            {
                mb.Instructions.RemoveAt(i);
            }
            mb.ExceptionHandlers.Remove(eh);
        }

        public void Debug_ManipulateStuff()
        {
            // Todo: Find out what this does.
            var ge = Module.GetType("Game.GnomanEmpire");
            var draw = ge.Methods.Single(m => m.Name == "Draw");
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[1], draw.Body);
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[0], draw.Body);
            //return;
            /*
             *
             * Off for now. Players reported crashes, e.g. when switching from fullscreen to windowed
             *
         * Update: Should be handled by first chance exceptions now anyway.
         *
            var ge = Module.GetType("Game.GnomanEmpire");
            var draw = ge.Methods.Single(m => m.Name == "Draw");
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[1], draw.Body);
            Debug_RemoveExceptionHandler(draw.Body.ExceptionHandlers[0], draw.Body);
             *
            return;*/
        }
    }

}
