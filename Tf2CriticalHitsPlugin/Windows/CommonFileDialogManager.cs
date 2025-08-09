using System;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Bindings.ImGui;

namespace Tf2CriticalHitsPlugin.Windows;

public class CommonFileDialogManager
{
    public static readonly FileDialogManager DialogManager = new()
    {
        AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking
                           
    };

    static CommonFileDialogManager()
    {
        DialogManager.CustomSideBarItems.Add((Environment.ExpandEnvironmentVariables("User Folder"),
                                                 Environment.ExpandEnvironmentVariables("%USERPROFILE%"),
                                                 FontAwesomeIcon.User, 0));
    }

}
