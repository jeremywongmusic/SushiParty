using System;
using UnityEngine;

namespace SushiParty.Core
{
    [Serializable]
    public sealed class MinigameDefinition
    {
        [SerializeField] private MinigameId id;
        [SerializeField] private string displayName;
        [SerializeField] private string sceneName;
        [SerializeField] private string objective;
        [SerializeField] private string roleP1;
        [SerializeField] private string roleP2;
        [SerializeField] private float timeLimit;
        [SerializeField] private bool implemented;
        [SerializeField] private Color accent;
        [SerializeField] private string designNotes;
        public MinigameId Id => id;
        public string DisplayName => displayName;
        public string SceneName => sceneName;
        public string Objective => objective;
        public string RoleP1 => roleP1;
        public string RoleP2 => roleP2;
        public float TimeLimit => timeLimit;
        public bool Implemented => implemented;
        public Color Accent => accent;
        public string DesignNotes => designNotes;

        public MinigameDefinition(
            MinigameId id,
            string displayName,
            string sceneName,
            string objective,
            string roleP1,
            string roleP2,
            float timeLimit,
            bool implemented,
            Color accent,
            string designNotes)
        {
            this.id = id;
            this.displayName = displayName;
            this.sceneName = sceneName;
            this.objective = objective;
            this.roleP1 = roleP1;
            this.roleP2 = roleP2;
            this.timeLimit = timeLimit;
            this.implemented = implemented;
            this.accent = accent;
            this.designNotes = designNotes;
        }

        public bool IsAsymmetric => !string.Equals(roleP1, roleP2, StringComparison.Ordinal);
    }
}
