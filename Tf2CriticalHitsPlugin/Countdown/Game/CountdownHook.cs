#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Tf2CriticalHitsPlugin.Countdown.Status;

// Shamefully copied from https://github.com/xorus/EngageTimer
/*
 * Based on the work (for finding the pointer) of https://github.com/Haplo064/Europe
 */
namespace Tf2CriticalHitsPlugin.Countdown.Game;

public sealed class CountdownHook : IDisposable
{
    private readonly ICondition condition;

    [Signature("40 53 48 83 EC 40 80 79 38 00", DetourName = nameof(CountdownTimerFunc))]
    private readonly Hook<CountdownTimerDelegate>? countdownTimerHook = null;

    private readonly State state;

    private ulong countDown;
    private bool countDownRunning;

    /// <summary>
    ///     Ticks since the timer stalled
    /// </summary>
    private int countDownStallTicks;

    private float lastCountDownValue;


    public CountdownHook(State state, ICondition condition)
    {
        this.state = state;
        this.condition = condition;
        countDown = 0;
        Service.GameInteropProvider.InitializeFromAttributes(this);
        countdownTimerHook?.Enable();
    }

    public void Dispose()
    {
        if (countdownTimerHook == null) return;
        countdownTimerHook.Disable();
        countdownTimerHook.Dispose();
    }

    private IntPtr CountdownTimerFunc(ulong value)
    {
        countDown = value;
        return countdownTimerHook!.Original(value);
    }

    public void Update()
    {
        if (state.Mocked) return;
        UpdateCountDown();
        state.InInstance = condition[ConditionFlag.BoundByDuty];
    }

    private void UpdateCountDown()
    {
        state.CountingDown = false;
        if (countDown == 0) return;
        var countDownPointerValue = Marshal.PtrToStructure<float>((IntPtr)countDown + 0x2c);

        // is last value close enough (workaround for floating point approx)
        if (Math.Abs(countDownPointerValue - lastCountDownValue) < 0.001f)
        {
            countDownStallTicks++;
        }
        else
        {
            countDownStallTicks = 0;
            countDownRunning = true;
        }
        if (countDownStallTicks > 50)
        {
            if (countDownRunning)
            {
                state.FireStopCountingDown(countDownPointerValue > 0.1f);
            }
            countDownRunning = false;
        }

        if (countDownPointerValue > 0 && countDownRunning)
        {
            var newValue = Marshal.PtrToStructure<float>((IntPtr)countDown + 0x2c);
            var oldCountdownValue = state.CountDownValue;
            state.CountDownValue = newValue;
            state.CountingDown = true;
            if (newValue > oldCountdownValue) state.FireStartCountingDown();
        }

        lastCountDownValue = countDownPointerValue;
    }

    [UnmanagedFunctionPointer(CallingConvention.ThisCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr CountdownTimerDelegate(ulong p1);
}
