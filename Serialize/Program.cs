using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using MessagePack;
using Newtonsoft.Json;
using SerializatorFox;
using System.Numerics;
using Test;

// Запуск: dotnet run -c Release
BenchmarkRunner.Run<SerializationBenchmarks>();
BenchmarkRunner.Run<DeserializationBenchmarks>();
PayloadSizeReport.Print();


// ════════════════════════════════════════════════════════════
//  Вспомогательные MessagePack-модели (без полиморфизма IItem)
// ════════════════════════════════════════════════════════════

[MessagePackObject(true)]
public class MP_PlayerData
{
    public string Nickname { get; set; }
    public int Level { get; set; }
    public float Health { get; set; }
    public MP_Vector3 Position { get; set; }
    public MP_Inventory PlayerInventory { get; set; }
    public Dictionary<string, MP_Achievement> Achievements { get; set; }
}

[MessagePackObject(true)]
public class MP_Vector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

[MessagePackObject(true)]
public class MP_Inventory
{
    public int MaxSlots { get; set; }
    public List<MP_Item> Items { get; set; }
}

[MessagePackObject(true)]
public class MP_Item
{
    public string Name { get; set; }
    public int Count { get; set; }
    public float Weight { get; set; }
    public int Type { get; set; }
    public string QuestId { get; set; }
    public bool IsCompleted { get; set; }
}

[MessagePackObject(true)]
public class MP_Achievement
{
    public string Title { get; set; }
    public string Description { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime UnlockDate { get; set; }
    public int Progress { get; set; }
    public int MaxProgress { get; set; }
}

// ════════════════════════════════════════════════════════════
//  Фабрика тестовых данных
// ════════════════════════════════════════════════════════════

public static class BenchmarkFixtures
{
    public static PlayerData CreateFoxPlayer() => new PlayerData
    {
        nickname = "FoxPlayer",
        level = 42,
        health = 100,
        Position = new Vector3(10, 20, 30),
        playerInventory = new Inventory
        {
            maxSlots = 10,
            items = new List<IItem>
            {
                new Item { Name = "Steel Sword", Count = 1, Weight = 5.0f, Type = ItemType.Weapon },
                new QuestItem { Name = "Ancient Artifact", Count = 1, Weight = 2.0f, Type = ItemType.Quest, QuestId = "QUEST_001", IsCompleted = false }
            }
        },
        achievements = new Dictionary<string, Achievement>
        {
            ["FirstBattle"] = new() { Title = "Первый бой", Description = "Участвуй в первом сражении", IsUnlocked = true, UnlockDate = new DateTime(2024, 1, 15), Progress = 1, MaxProgress = 1 },
            ["ItemCollector"] = new() { Title = "Коллекционер", Description = "Собери 100 предметов", IsUnlocked = false, Progress = 45, MaxProgress = 100 },
            ["DragonSlayer"] = new() { Title = "Убийца драконов", Description = "Победи древнего дракона", IsUnlocked = false, Progress = 0, MaxProgress = 1 },
            ["WealthyMerchant"] = new() { Title = "Богатый торговец", Description = "Накопи 10000 золота", IsUnlocked = false, Progress = 3500, MaxProgress = 10000 },
            ["ExplorerNovice"] = new() { Title = "Начинающий исследователь", Description = "Исследуй 5 новых локаций", IsUnlocked = true, UnlockDate = new DateTime(2024, 1, 20), Progress = 5, MaxProgress = 5 },
            ["QuestMaster"] = new() { Title = "Мастер заданий", Description = "Выполни 50 квестов", IsUnlocked = false, Progress = 23, MaxProgress = 50 },
            ["ArmorCollector"] = new() { Title = "Бронированный", Description = "Собери все виды брони", IsUnlocked = false, Progress = 3, MaxProgress = 10 },
            ["MagicMaster"] = new() { Title = "Мастер магии", Description = "Изучи 20 заклинаний", IsUnlocked = false, Progress = 8, MaxProgress = 20 },
            ["Survivor"] = new() { Title = "Выживший", Description = "Выживи 30 дней", IsUnlocked = true, UnlockDate = new DateTime(2024, 2, 1), Progress = 30, MaxProgress = 30 },
            ["Fisherman"] = new() { Title = "Рыбак", Description = "Поймай 50 рыб", IsUnlocked = false, Progress = 12, MaxProgress = 50 }
        }
    };

    public static MP_PlayerData CreateMPPlayer() => new MP_PlayerData
    {
        Nickname = "FoxPlayer",
        Level = 42,
        Health = 100,
        Position = new MP_Vector3 { X = 10, Y = 20, Z = 30 },
        PlayerInventory = new MP_Inventory
        {
            MaxSlots = 10,
            Items = new List<MP_Item>
            {
                new() { Name = "Steel Sword",     Count = 1, Weight = 5.0f, Type = 0 },
                new() { Name = "Ancient Artifact", Count = 1, Weight = 2.0f, Type = 3, QuestId = "QUEST_001", IsCompleted = false }
            }
        },
        Achievements = new Dictionary<string, MP_Achievement>
        {
            ["FirstBattle"] = new() { Title = "Первый бой", Description = "Участвуй в первом сражении", IsUnlocked = true, UnlockDate = new DateTime(2024, 1, 15), Progress = 1, MaxProgress = 1 },
            ["ItemCollector"] = new() { Title = "Коллекционер", Description = "Собери 100 предметов", IsUnlocked = false, Progress = 45, MaxProgress = 100 },
            ["DragonSlayer"] = new() { Title = "Убийца драконов", Description = "Победи древнего дракона", IsUnlocked = false, Progress = 0, MaxProgress = 1 },
            ["WealthyMerchant"] = new() { Title = "Богатый торговец", Description = "Накопи 10000 золота", IsUnlocked = false, Progress = 3500, MaxProgress = 10000 },
            ["ExplorerNovice"] = new() { Title = "Начинающий исследователь", Description = "Исследуй 5 новых локаций", IsUnlocked = true, UnlockDate = new DateTime(2024, 1, 20), Progress = 5, MaxProgress = 5 },
            ["QuestMaster"] = new() { Title = "Мастер заданий", Description = "Выполни 50 квестов", IsUnlocked = false, Progress = 23, MaxProgress = 50 },
            ["ArmorCollector"] = new() { Title = "Бронированный", Description = "Собери все виды брони", IsUnlocked = false, Progress = 3, MaxProgress = 10 },
            ["MagicMaster"] = new() { Title = "Мастер магии", Description = "Изучи 20 заклинаний", IsUnlocked = false, Progress = 8, MaxProgress = 20 },
            ["Survivor"] = new() { Title = "Выживший", Description = "Выживи 30 дней", IsUnlocked = true, UnlockDate = new DateTime(2024, 2, 1), Progress = 30, MaxProgress = 30 },
            ["Fisherman"] = new() { Title = "Рыбак", Description = "Поймай 50 рыб", IsUnlocked = false, Progress = 12, MaxProgress = 50 }
        }
    };
}

// ════════════════════════════════════════════════════════════
//  Бенчмарк сериализации
// ════════════════════════════════════════════════════════════

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class SerializationBenchmarks
{
    private PlayerData _foxPlayer = null!;
    private MP_PlayerData _mpPlayer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _foxPlayer = BenchmarkFixtures.CreateFoxPlayer();
        _mpPlayer = BenchmarkFixtures.CreateMPPlayer();

    }

    [Benchmark(Description = "Fox · без сжатия")]
    public byte[] Fox_NoCompression()
    {
        using var s = new ByteSerialeze(false);
        s.Serialize(_foxPlayer);
        return s.GetData();
    }

    [Benchmark(Description = "Fox · со сжатием")]
    public byte[] Fox_Compressed()
    {
        using var s = new ByteSerialeze(true);
        s.Serialize(_foxPlayer);
        return s.GetData();
    }

    [Benchmark(Description = "MessagePack · без сжатия")]
    public byte[] MsgPack_NoCompression()
        => MessagePackSerializer.Serialize(_mpPlayer);

    [Benchmark(Description = "MessagePack · LZ4")]
    public byte[] MsgPack_LZ4()
        => MessagePackSerializer.Serialize(_mpPlayer,
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

    [Benchmark(Description = "Newtonsoft.Json")]
    public string Newtonsoft()
        => JsonConvert.SerializeObject(_foxPlayer);
}

// ════════════════════════════════════════════════════════════
//  Бенчмарк десериализации
// ════════════════════════════════════════════════════════════

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class DeserializationBenchmarks
{
    private byte[] _foxRaw = null!;
    private byte[] _foxCompressed = null!;
    private byte[] _mpRaw = null!;
    private byte[] _mpLZ4 = null!;
    private string _newtonsoftJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        var foxPlayer = BenchmarkFixtures.CreateFoxPlayer();
        var mpPlayer = BenchmarkFixtures.CreateMPPlayer();

        var settingsJSON = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        using (var s = new ByteSerialeze(false)) { s.Serialize(foxPlayer); _foxRaw = s.GetData(); }
        using (var s = new ByteSerialeze(true)) { s.Serialize(foxPlayer); _foxCompressed = s.GetData(); }

        _mpRaw = MessagePackSerializer.Serialize(mpPlayer);
        _mpLZ4 = MessagePackSerializer.Serialize(mpPlayer,
                            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        _newtonsoftJson = JsonConvert.SerializeObject(foxPlayer, settingsJSON);
    }

    [Benchmark(Description = "Fox · без сжатия")]
    public PlayerData Fox_NoCompression()
    {
        using var d = new ByteDeserializer(_foxRaw);
        return d.Deserialize<PlayerData>();
    }

    [Benchmark(Description = "Fox · со сжатием")]
    public PlayerData Fox_Compressed()
    {
        using var d = new ByteDeserializer(_foxCompressed);
        return d.Deserialize<PlayerData>();
    }

    [Benchmark(Description = "MessagePack · без сжатия")]
    public MP_PlayerData MsgPack_NoCompression()
        => MessagePackSerializer.Deserialize<MP_PlayerData>(_mpRaw);

    [Benchmark(Description = "MessagePack · LZ4")]
    public MP_PlayerData MsgPack_LZ4()
        => MessagePackSerializer.Deserialize<MP_PlayerData>(_mpLZ4,
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

    [Benchmark(Description = "Newtonsoft.Json")]
    public PlayerData? Newtonsoft()
        => JsonConvert.DeserializeObject<PlayerData>(_newtonsoftJson);
}

// ════════════════════════════════════════════════════════════
//  Отчёт по размеру payload (не бенчмарк, просто вывод)
// ════════════════════════════════════════════════════════════

public static class PayloadSizeReport
{
    public static void Print()
    {
        var fox = BenchmarkFixtures.CreateFoxPlayer();
        var mp = BenchmarkFixtures.CreateMPPlayer();

        byte[] foxRaw, foxComp, mpRaw, mpLz4;
        string json;

        using (var s = new ByteSerialeze(false)) { s.Serialize(fox); foxRaw = s.GetData(); }
        using (var s = new ByteSerialeze(true)) { s.Serialize(fox); foxComp = s.GetData(); }

        mpRaw = MessagePackSerializer.Serialize(mp);
        mpLz4 = MessagePackSerializer.Serialize(mp,
                    MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        json = JsonConvert.SerializeObject(fox);

        Console.WriteLine("\n=== Payload Size Report ===");
        Console.WriteLine($"{"Serializer",-35} {"Bytes",7}");
        Console.WriteLine(new string('─', 44));
        Console.WriteLine($"{"Fox (no compression)",-35} {foxRaw.Length,7}");
        Console.WriteLine($"{"Fox (compressed)",-35} {foxComp.Length,7}");
        Console.WriteLine($"{"MessagePack (no compression)",-35} {mpRaw.Length,7}");
        Console.WriteLine($"{"MessagePack (LZ4)",-35} {mpLz4.Length,7}");
        Console.WriteLine($"{"Newtonsoft.Json",-35} {System.Text.Encoding.UTF8.GetByteCount(json),7}");
    }
}