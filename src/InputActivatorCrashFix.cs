using System.Runtime.InteropServices;
using SwiftlyS2.Shared.Memory;
using SwiftlyS2.Shared.Natives;

namespace Fixes;

[StructLayout(LayoutKind.Sequential)]
public struct InputData_t
{
    public nint Activator;
    public nint Caller;
    public CVariant<CVariantDefaultAllocator> Value;
    public int OutputID;
}

public partial class Fixes
{
    private unsafe delegate nint CBaseFilter_InputTestActivatorDelegateLinux(nint pEntity, InputData_t* inputData);

    private IUnmanagedFunction<CBaseFilter_InputTestActivatorDelegateLinux>? _CBaseFilter_InputTestActivatorDelegateLinux;
    private bool enableInputActivatorCrashFix = false;

    public void InitInputActivatorCrashFix()
    {
        
        enableInputActivatorCrashFix = Config.CurrentValue.EnableInputActivatorCrashFix;
        Config.OnChange((v, _) =>
        {
            enableInputActivatorCrashFix = v.EnableInputActivatorCrashFix;
        });

        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
        } 
        else
        {
            _CBaseFilter_InputTestActivatorDelegateLinux = Core.Memory.GetUnmanagedFunctionByAddress<CBaseFilter_InputTestActivatorDelegateLinux>(
                Core.GameData.GetSignature("CBaseFilter::InputTestActivator")
            );

            _CBaseFilter_InputTestActivatorDelegateLinux.AddHook(next =>
            {
                unsafe
                {
                    return (pEntity, inputData) =>
                    {
                        if (enableInputActivatorCrashFix)
                        {
                            if (inputData->Activator == 0) return 0;
                        }

                        return next()(pEntity, inputData);
                    };
                }
            });
        }
    }

}