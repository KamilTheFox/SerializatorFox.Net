using System.Numerics;
using SerializatorFox;

namespace Test
{
    [SerializableType]
    [System.Serializable]
    public class PlayerData
    {
        public string nickname;
        public string прOзвище;
        public int level;
        public float health;

        public Dictionary<string, Achievement> achievements;

        [field: IgnoreSerialize]
        public Vector3 Position { get; set; }

        [SerializeDepth(true)] // Глубокая сериализация для инвентаря
        public Inventory playerInventory;
        public override string ToString()
        {
            var achievementsString = achievements == null
            ? "\n    No achievements"
            : string.Join("", achievements.Select(kvp =>
            $"\n    ├─ {kvp.Key}" +
            $"\n    │  ├─ Title: {kvp.Value.Title}" +
            $"\n    │  ├─ Progress: {kvp.Value.Progress}/{kvp.Value.MaxProgress}" +
            $"\n    │  ├─ Status: {(kvp.Value.IsUnlocked ? "✓ Completed" : "⋯ In Progress")}" +
            $"\n    │  └─ Unlock Date: {(kvp.Value.IsUnlocked ? kvp.Value.UnlockDate.ToString("d") : "Not yet")}"
            ));
            return $"\nPlayer Data:" +
                   $"\n  Nickname: {nickname}" +
                   $"\n  Прозвище: {прOзвище}" +
                   $"\n  Level: {level}" +
                   $"\n  Health: {health:F1}" +
                   $"\n  Position: {Position} (non-serialized)" +
                   $"\n  Inventory: {playerInventory}" +
                   $"\n  Achievements ({achievements?.Count ?? 0}):{achievementsString}";
        }
    }

    [SerializableType]
    [System.Serializable]
    public class Inventory
    {
        public List<IItem> items;
        public int maxSlots;
        public override string ToString()
        {
            return $"\nInventory:" +
                   $"\n  Max Slots: {maxSlots}" +
                   $"\n  Items ({(items?.Count ?? 0)}):" +
                   $"{(items == null ? "\n    No items" : string.Join("", items?.Select(item => $"\n    {item}") ?? Array.Empty<string>()))}";
        }
    }

    public interface IItem
    {
        string Name { get; set; }
        int Count { get; set; }
        float Weight { get; set; }
        ItemType Type { get; set; }
    }

    [SerializableType]
    [System.Serializable]
    public class Item : IItem
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public float Weight { get; set; }
        public ItemType Type { get; set; }

        public override string ToString()
        {
            return $"[{Type}] {Name} x{Count} (Weight: {Weight:F1})";
        }
    }
    [SerializableType]
    [System.Serializable]
    public class QuestItem : IItem
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public float Weight { get; set; }
        public ItemType Type { get; set; }

        // Тестирую уникальные свойства
        public string QuestId { get; set; }
        public bool IsCompleted { get; set; }

        public override string ToString()
        {
            return $"[QUEST:{Type}] {Name} x{Count} (Weight: {Weight:F1}) - Quest: {QuestId} {(IsCompleted ? "[✓]" : "[...]")}";
        }
    }
    [SerializableType]
    [System.Serializable]
    public class Achievement
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsUnlocked { get; set; }
        public DateTime UnlockDate { get; set; }
        public int Progress { get; set; }
        public int MaxProgress { get; set; }

        public override string ToString()
        {
            return $"{Title} - {Progress}/{MaxProgress} {(IsUnlocked ? "✓" : "...")} [{UnlockDate:d}]";
        }
    }
    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        Quest
    }
}