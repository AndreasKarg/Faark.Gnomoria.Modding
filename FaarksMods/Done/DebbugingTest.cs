using System.Diagnostics;

// This stuff makes your mod-dll live-editable while debugging it via you debug-launcher. Does not always work perfect, though.
[assembly: Debuggable(
      DebuggableAttribute.DebuggingModes.Default
    | DebuggableAttribute.DebuggingModes.DisableOptimizations
    | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints
    | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)
]

