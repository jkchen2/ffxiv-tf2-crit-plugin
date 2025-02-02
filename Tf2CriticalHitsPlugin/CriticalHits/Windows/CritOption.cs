using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using KamiLib;
using KamiLib.Configuration;
using KamiLib.Drawing;
using KamiLib.Interfaces;
using Lumina.Excel.Sheets;
using Tf2CriticalHitsPlugin.Common.Windows;
using Tf2CriticalHitsPlugin.Configuration;
using Tf2CriticalHitsPlugin.CriticalHits.Configuration;
using Tf2CriticalHitsPlugin.SeFunctions;
using Tf2CriticalHitsPlugin.Windows;

namespace Tf2CriticalHitsPlugin.CriticalHits.Windows;

public class CritOption : ISelectable, IDrawable
{
    internal readonly CriticalHitsConfigOne.JobConfig JobConfig;
    private readonly FileDialogManager dialogManager;

    internal CritOption(CriticalHitsConfigOne.JobConfig jobConfig, FileDialogManager dialogManager)
    {
        this.JobConfig = jobConfig;
        this.dialogManager = dialogManager;
    }


    public IDrawable Contents => this;

    public void DrawLabel()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, GetJobColor(JobConfig.GetClassJob()));
        ImGui.Text(JobConfig.GetClassJob().NameEnglish.ExtractText());
        ImGui.PopStyleColor();
    }

    public string ID => JobConfig.GetClassJob().Abbreviation.ExtractText();

    public void Draw()
    {
        DrawDetailPane(JobConfig, dialogManager);
    }

    private static void DrawDetailPane(CriticalHitsConfigOne.JobConfig jobConfig, FileDialogManager dialogManager)
    {
        ImGui.Text($"Configuration for {jobConfig.GetClassJob().NameEnglish}");
        InfoBox.Instance.AddTitle("General")
               .AddString("Play sounds once every")
               .SameLine()
               .AddInputInt("ms", jobConfig.TimeBetweenSounds, 0, 100000, width: 200F)
               .Draw();
        foreach (var module in CriticalHitsConfigOne.GetModules(jobConfig))
        {
            if (module.ModuleType.Value == ModuleType.OwnFairyCriticalHeal &&
                jobConfig.GetClassJob().Abbreviation.ToString() != "SCH")
            {
                continue;
            }
            DrawConfigModule(module, dialogManager);
        }
    }

    private static void DrawConfigModule(CriticalHitsConfigOne.ConfigModule config, FileDialogManager dialogManager)
    {
        InfoBox.Instance.AddTitle(config.GetModuleDefaults().SectionLabel)
               .StartConditional(config.GetModuleDefaults().SectionNote is not null)
               .AddString(config.GetModuleDefaults().SectionNote ?? "", Colors.Orange)
               .EndConditional()
               .StartConditional(config.ModuleType == ModuleType.DirectDamage)
               .AddConfigCheckbox("Apply for PvP attacks", config.ApplyInPvP,
                                  "Some Jobs show all their damage output as Direct Damage in PvP." +
                                  "\nCheck this to have the Direct Damage configuration trigger on every attack in PvP.")
               .EndConditional()
               .AddConfigCheckbox("Use custom file", config.UseCustomFile, additionalID: $"{config.GetId()}PlaySound")
               .StartConditional(!config.UseCustomFile)
               .AddIndent(2)
               .AddConfigCheckbox("Play sound only for actions (ignore auto-attacks)", config.SoundForActionsOnly)
               .AddConfigCombo(SoundsExtensions.Values(), config.GameSound, s => s.ToName(), width: 7.5F * ImGui.GetFontSize())
               .SameLine()
               .AddIconButton($"{config.GetId()}testSfx", FontAwesomeIcon.Play,
                              () => UIGlobals.PlaySoundEffect((uint)config.GameSound.Value))
               .SameLine()
               .AddString("(Volume is controlled by the game's settings)")
               .AddIndent(-2)
               .EndConditional()
               .StartConditional(config.UseCustomFile)
               .AddIndent(2)
               .AddConfigCheckbox("Play sound only for actions (ignore auto-attacks)", config.SoundForActionsOnly)
               .AddSoundFileConfiguration(config.GetId(), config.FilePath, config.Volume, config.ApplySfxVolume, dialogManager)
               .AddIndent(-2)
               .EndConditional()
               .AddConfigCheckbox("Show flavor text with floating value", config.ShowText)
               .StartConditional(config.ShowText)
               .AddIndent(2)
               .AddInputString("Text", config.Text, Constants.MaxTextLength)
               .AddAction(() => ImGui.Text("Color: "))
               .SameLine()
               .AddAction(() =>
               {
                   if (ColorComponent.SelectorButton(CritTab.ForegroundColors, $"{config.GetId()}Foreground",
                                                     ref config.TextColor.Value,
                                                     config.GetModuleDefaults().FlyTextParameters.ColorKey.Value))
                   {
                       KamiCommon.SaveConfiguration();
                   }
               })
               .SameLine()
               .AddAction(() => ImGui.Text("Glow: "))
               .SameLine()
               .AddAction(() =>
               {
                   if (ColorComponent.SelectorButton(CritTab.GlowColors, $"{config.GetId()}Glow",
                                                     ref config.TextGlowColor.Value,
                                                     config.GetModuleDefaults().FlyTextParameters.GlowColorKey.Value))
                   {
                       KamiCommon.SaveConfiguration();
                   }
               })
               .SameLine()
               .AddConfigCheckbox("Italics", config.TextItalics)
               .AddIndent(-2)
               .EndConditional()
               .AddButton("Test configuration", () => CriticalHitsModule.GenerateTestFlyText(config))
               .Draw();
    }


    private static Vector4 GetJobColor(ClassJob classJob) => classJob.Role switch
    {
        1 => Colors.Blue,
        4 => Colors.HealerGreen,
        _ => Colors.DPSRed
    };
}
