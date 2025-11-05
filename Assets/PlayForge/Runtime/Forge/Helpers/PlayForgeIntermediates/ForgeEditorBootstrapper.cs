using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [InitializeOnLoad]
    public static class ForgeEditorBootstrapper
    {

        static ForgeEditorBootstrapper()
        {
            var ms = ForgeStores.LoadMasterSettings();
            if (ms.Status(ForgeTags.Settings.PROMPT_WHEN_INITIALIZE_EDITOR))
            {
                
            }
        }
    }
}
