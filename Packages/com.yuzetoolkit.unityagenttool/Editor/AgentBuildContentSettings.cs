#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit.UnityAgent
{
    /// <summary>
    /// Read-only build summary used by Editor settings surfaces. Build inclusion is represented by
    /// the two ordered root lists in the active machine settings. Effective package/project defaults
    /// seed a machine settings file when it is missing or invalid.
    /// </summary>
    internal sealed class AgentBuildContentView : VisualElement
    {
        private readonly VisualElement _rootList;

        public AgentBuildContentView(UnityAgentHost host)
        {
            _ = host ?? throw new ArgumentNullException(nameof(host));
            style.flexShrink = 0;
            style.paddingLeft = 12;
            style.paddingRight = 12;
            style.paddingTop = 7;
            style.paddingBottom = 9;
            style.borderTopWidth = 1;
            style.borderTopColor = new Color(0.18f, 0.21f, 0.25f);
            style.backgroundColor = new Color(0.055f, 0.068f, 0.085f);

            var title = new Label("Player build instruction content");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(title);
            var help = new Label(
                "Configured AGENTS.md and Skill roots are packaged in their displayed order. " +
                "The default project entries are explicit settings and may be reordered or removed.");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.color = new Color(0.52f, 0.56f, 0.62f);
            Add(help);
            _rootList = new VisualElement();
            _rootList.style.marginTop = 4;
            Add(_rootList);
        }

        public void Refresh(AgentSettingsDocument settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _rootList.Clear();
            AddRoots("AGENTS.md", settings.AgentsRoots, isSkillRoot: false);
            AddRoots("Skills", settings.SkillRoots, isSkillRoot: true);
        }

        private void AddRoots(
            string heading,
            IReadOnlyList<AgentPathLocation> roots,
            bool isSkillRoot)
        {
            var label = new Label(heading);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 4;
            _rootList.Add(label);
            if (roots.Count == 0)
            {
                var empty = new Label("No roots configured.");
                empty.style.color = new Color(0.52f, 0.56f, 0.62f);
                _rootList.Add(empty);
                return;
            }

            for (var index = 0; index < roots.Count; index++)
            {
                var root = roots[index];
                var relative = string.IsNullOrEmpty(root.RelativePath) ? "." : root.RelativePath;
                var buildState = root.IncludeInPlayerBuild ? "included" : "Editor only";
                var fixedPath = isSkillRoot
                    ? $"{AgentPaths.SettingsDirectoryName} / {AgentPaths.SkillDirectoryName}"
                    : AgentPaths.SettingsDirectoryName;
                var item = new Label($"{index + 1}. {root.BasePath} / {fixedPath} / {relative}  ·  {buildState}");
                AgentTooltip.Attach(item, isSkillRoot ? AgentPaths.ResolveSkill(root) : AgentPaths.Resolve(root));
                item.style.color = new Color(0.72f, 0.76f, 0.82f);
                _rootList.Add(item);
            }
        }
    }
}
