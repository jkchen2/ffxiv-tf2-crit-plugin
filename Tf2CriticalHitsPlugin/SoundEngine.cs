﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dalamud.Logging;
using Dalamud.Utility;
using NAudio.Wave;
using Tf2CriticalHitsPlugin.Common;

namespace Tf2CriticalHitsPlugin;

public static class SoundEngine
{
    private static readonly IDictionary<string, byte> SoundState = new ConcurrentDictionary<string, byte>();

    public static bool IsPlaying(string id)
    {
        return SoundState.ContainsKey(id);
    }

    public static void StopSound(string id)
    {
        SoundState.Remove(id);
    }


    public static void StopSoundsWithIdStartingWith(string countdown)
    {
        foreach (var s in SoundState.Keys.Where(k => k.StartsWith(countdown)).ToArray())
        {
            SoundState.Remove(s);
        }
    }
    // Copied from PeepingTom plugin, by ascclemens:
    // https://git.anna.lgbt/ascclemens/PeepingTom/src/commit/3749a6b42154a51397733abb2d3b06a47915bdcc/Peeping%20Tom/TargetWatcher.cs#L162
    public static void PlaySound(string? path, bool useGameSfxVolume, int volume = 100, string? id = null)
    {
        if (path.IsNullOrEmpty() || !File.Exists(path))
        {
            Service.PluginLog.Error($"Could not find audio file: [{path}]");
            return;
        }

        var soundDevice = DirectSoundOut.DSDEVID_DefaultPlayback;
        new Thread(() =>
        {
            WaveStream reader;
            try
            {
                if (Util.IsWine() && path.EndsWith(".wav"))
                {
                    Service.PluginLog.Debug($"On Linux, reading .wav: {path}");
                    reader = new WaveFileReader(path);
                }
                else
                {
                    Service.PluginLog.Debug($"Reading file regularly: {path}");
                    reader = new MediaFoundationReader(path);
                }
            }
            catch (Exception e)
            {
                Service.PluginLog.Error(e.Message);
                return;
            }

            using (reader)
            {
                using var output = new DirectSoundOut(soundDevice);

                try
                {
                    using var channel = new WaveChannel32(reader)
                    {
                        Volume = GetVolume(volume, useGameSfxVolume),
                        PadWithZeroes = false,
                    };
                    output.Init(channel);
                    output.Play();
                    if (id is not null)
                    {
                        SoundState[id] = 1;
                    }

                    while (output.PlaybackState == PlaybackState.Playing)
                    {
                        if (id is not null && !SoundState.ContainsKey(id))
                        {
                            output.Stop();
                        }

                        Thread.Sleep(500);
                    }

                    if (id is not null)
                    {
                        SoundState.Remove(id);
                    }
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Exception playing sound");
                }
            }
        }).Start();
    }

    private static float GetVolume(int baseVolume, bool applyGameSfxVolume)
    {
        return (Math.Min(baseVolume, 100) * (applyGameSfxVolume ? GameSettings.GetEffectiveSfxVolume() : 1)) / 100f;
    }
}
