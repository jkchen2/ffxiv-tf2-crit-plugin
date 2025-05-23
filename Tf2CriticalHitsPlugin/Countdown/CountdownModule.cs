using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Tf2CriticalHitsPlugin.Countdown.Configuration;
using Tf2CriticalHitsPlugin.Countdown.Game;
using Tf2CriticalHitsPlugin.Countdown.Status;

namespace Tf2CriticalHitsPlugin.Countdown;

public class CountdownModule : IDisposable
{
    private readonly State state;
    private readonly CountdownConfigZero config;
    private readonly CountdownHook countdownHook;
    private readonly ISet<string> playedForCurrentCountdown = new HashSet<string>();

    public CountdownModule(State state, CountdownConfigZero config)
    {
        this.countdownHook = new CountdownHook(state, Service.Condition);
        this.state = state;
        this.config = config;
        state.StartCountingDown += OnStartCountingDown;
        state.StopCountingDown += OnStopCountingDown;
        Service.Framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        countdownHook.Update();
        if (!state.CountingDown) return;
        var firstModule = config.modules
                           .Where(m => m.Enabled)
                           .Where(m => m.ValidForCountdown(state.StartingValue))
                           .FirstOrDefault(m => m.ValidForTerritory(Service.ClientState.TerritoryType));

        if (firstModule is null) return;
        var otherModules = config.modules
                                 .Where(m => m.Enabled)
                                 .Where(m => m.DelayPlay)
                                 .Where(m => m.PlayWithOtherSounds)
                                 .Where(m => m.Id.Value != firstModule.Id.Value)
                                 .Where(m => m.ValidForCountdown(state.StartingValue))
                                 .Where(m => m.ValidForTerritory(Service.ClientState.TerritoryType));
        foreach (var module in new[] { firstModule }.Concat(otherModules))
        {
            if (!state.CountingDown || state.CountDownValue > module.DelayUntilCountdownHits.Value ||
                playedForCurrentCountdown.Contains(module.Id.Value)) continue;
            SoundEngine.PlaySound(module.FilePath.Value, module.ApplySfxVolume, module.Volume.Value, $"countdown|{module.Id}");
            playedForCurrentCountdown.Add(module.Id.Value);
        } 
    }

    private void OnStartCountingDown(object? sender, EventArgs args)
    {
        playedForCurrentCountdown.Clear();
        if (sender is null) return;
        var state = (State)sender;
        var module = config.modules
                           .Where(m => m.Enabled)
                           .Where(m => !m.DelayPlay)
                           .Where(m => m.ValidForCountdown(state.StartingValue))
                           .FirstOrDefault(m => m.ValidForTerritory(Service.ClientState.TerritoryType));

        if (module is null) return;
        SoundEngine.PlaySound(module.FilePath.Value, module.ApplySfxVolume, module.Volume.Value,
                              $"countdown|{module.Id}");
        playedForCurrentCountdown.Add(module.Id.Value);
    }

    private void OnStopCountingDown(object? sender, EventArgs e)
    {
        if (sender is null) return;
        var state = (State)sender;
        if (state.countdownCancelled)
        {
            var moduleToPlayInterrupt = config.modules
                                     .FirstOrDefault(m => SoundEngine.IsPlaying($"countdown|{m.Id}"));
            if (moduleToPlayInterrupt is null) return;

            SoundEngine.StopSoundsWithIdStartingWith("countdown|");
            SoundEngine.PlaySound(moduleToPlayInterrupt.InterruptedFilePath.Value, moduleToPlayInterrupt.InterruptedApplySfxVolume,
                                  moduleToPlayInterrupt.InterruptedVolume.Value, $"countdownstop");
        }
        else
        {
            foreach (var module in config.modules
                                    .Where(m => m.StopWhenCountdownCompletes)
                                    .Where(m => SoundEngine.IsPlaying($"countdown|{m.Id}")))
            {
                SoundEngine.StopSound($"countdown|{module.Id}");
            }
        }
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnUpdate;
        state.StartCountingDown -= OnStartCountingDown;
        state.StopCountingDown -= OnStopCountingDown;
        countdownHook.Dispose();
    }
}
