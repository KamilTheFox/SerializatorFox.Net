using System.Numerics;
using SerializatorFox;

namespace Test
{
    [SerializableType]
    public class PlayerData
    {
        public string nickname;
        public string прOзвище;
        public int level;
        public float health;

        [field: IgnoreSerialize]
        public Vector3 Position { get; set; }

        [SerializeDepth(true)] // Глубокая сериализация для инвентаря
        public Inventory playerInventory;
        public override string ToString()
        {
            return $"\nPlayer Data:" +
                   $"\n  Nickname: {nickname}" +
                   $"\n  Прозвище: {прOзвище}" +
                   $"\n  Level: {level}" +
                   $"\n  Health: {health:F1}" +
                   $"\n  Position: {Position} (non-serialized)" +
                   $"\n  Inventory: {playerInventory}";
        }
    }

    [SerializableType]
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

    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        Quest
    }
}