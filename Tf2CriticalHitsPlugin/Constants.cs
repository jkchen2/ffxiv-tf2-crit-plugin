using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Lumina.Excel.Sheets;
using Action = Lumina.Excel.Sheets.Action;

namespace Tf2CriticalHitsPlugin;

public abstract class Constants
{
    public const uint DamageColor = 4278215139;
    public const uint HealColor = 4278213930;
    public const uint IconButtonSize = 38;

    public const int MaxTextLength = 40;

    public static readonly string[] TestFlavorText =
        { "Stickybomb", "Meatshot 8)", "Crocket", "Lucksman", "360 Noscope", "Reflected Rocket", "Random crit lol" };

    public static readonly IDictionary<uint, ClassJob> CombatJobs;
    public static readonly ImmutableDictionary<uint, ISet<string>> ActionsPerJob;

    static Constants()
    {
        CombatJobs = InitCombatJobs();
        ActionsPerJob = InitActionPerJob();
    }

    private static IDictionary<uint, ClassJob> InitCombatJobs()
    {
        // A combat job is one that has a JobIndex.
        if (Service.DataManager == null) throw new ApplicationException("DataManager not initialized!");

        var classJobSheet = Service.DataManager.GetExcelSheet<ClassJob>();
        if (classJobSheet == null) throw new ApplicationException("ClassJob sheet unavailable!");
        var jobList = new List<ClassJob>();
        for (var i = 0u; i < classJobSheet.Count; i++)
        {
            var classJob = classJobSheet.GetRowOrDefault(i);
            if (classJob is null || classJob.Value.JobIndex is 0) continue;
            jobList.Add(classJob.Value);
        }

        return jobList.ToImmutableSortedDictionary(j => j.RowId, j => j);
    }

    private static ImmutableDictionary<uint, ISet<string>> InitActionPerJob()
    {
        if (Service.DataManager == null) throw new ApplicationException("DataManager not initialized!");
        var result = new Dictionary<uint, ISet<string>>();
        var actionSheet = Service.DataManager.GetExcelSheet<Action>();
        if (actionSheet is null) throw new ApplicationException("Action sheet unavailable!");
        foreach (var action in actionSheet)
        {
            if (!result.ContainsKey(action.ClassJob.RowId))
            {
                result[action.ClassJob.RowId] = new HashSet<string>();
            }

            result[action.ClassJob.RowId].Add(action.Name.ExtractText());
        }

        var classJobSheet = Service.DataManager.GetExcelSheet<ClassJob>();
        
        // Add the parent Class's action to a Job action
        // (for when your White Mage *insists* on using Cure I)
        foreach (var classJob in classJobSheet!)
        {
            if (classJob.ClassJobParent.Value.RowId != classJob.RowId)
            {
                foreach (var parentAction in result[classJob.ClassJobParent.Value.RowId])
                {
                    if (!result.ContainsKey(classJob.RowId))
                    {
                        result[classJob.RowId] = new HashSet<string>();
                    }
                    result[classJob.RowId].Add(parentAction);
                }
            }
        }
        
        // Second Wind special case 🌈
        var secondWind = actionSheet.GetRowOrDefault(57);
        if (secondWind is not null)
        {
            foreach (var classJob in classJobSheet.Where(c => c.Role is 2 or 3))
            {
                result[classJob.RowId].Add(secondWind.Value.Name.ExtractText());
            }
        }
        
        // Bloodbath super special case 🌈
        var bloodbath = actionSheet.GetRowOrDefault(34);
        if (bloodbath is not null)
        {
            // Role 3 includes magical DPS, but eh
            foreach (var classJob in classJobSheet.Where(c => c.Role is 3))
            {
                result[classJob.RowId].Add(bloodbath.Value.Name.ExtractText());
            }
        }

        foreach (var (key, value) in result)
        {
            result[key] = value.ToImmutableHashSet();
        }

        return result.ToImmutableDictionary();
    }
}
