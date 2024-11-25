using Test;
using System.Reflection;
using System.Numerics;
using SerializatorFox;

Console.WriteLine("Создание тестового объекта с вложениями из сторонних пространств имен!");

var player = new PlayerData
{
    nickname = "FoxPlayer",
    level = 42,
    health = 100,
    Position = new Vector3(10, 20, 30),
    playerInventory = new Inventory()
    {
        maxSlots = 10,
        items = new List<IItem>
        {
            new Item { Name = "Steel Sword", Count = 1, Weight = 5.0f, Type = ItemType.Weapon },
            new QuestItem
            {
                Name = "Ancient Artifact",
                Count = 1,
                Weight = 2.0f,
                Type = ItemType.Quest,
                QuestId = "QUEST_001",
                IsCompleted = false
            }
        }
    },
    achievements = new()
        {
            {
                "FirstBattle",
                new Achievement
                {
                    Title = "Первый бой",
                    Description = "Участвуй в первом сражении",
                    IsUnlocked = true,
                    UnlockDate = new DateTime(2024, 1, 15),
                    Progress = 1,
                    MaxProgress = 1
                }
            },
            {
                "ItemCollector",
                new Achievement
                {
                    Title = "Коллекционер",
                    Description = "Собери 100 предметов",
                    IsUnlocked = false,
                    Progress = 45,
                    MaxProgress = 100
                }
            },
            {
                "DragonSlayer",
                new Achievement
                {
                    Title = "Убийца драконов",
                    Description = "Победи древнего дракона",
                    IsUnlocked = false,
                    Progress = 0,
                    MaxProgress = 1
                }
            },
            {
                "WealthyMerchant",
                new Achievement
                {
                    Title = "Богатый торговец",
                    Description = "Накопи 10000 золота",
                    IsUnlocked = false,
                    Progress = 3500,
                    MaxProgress = 10000
                }
            },
            {
                "ExplorerNovice",
                new Achievement
                {
                    Title = "Начинающий исследователь",
                    Description = "Исследуй 5 новых локаций",
                    IsUnlocked = true,
                    UnlockDate = new DateTime(2024, 1, 20),
                    Progress = 5,
                    MaxProgress = 5
                }
            },
            {
                "QuestMaster",
                new Achievement
                {
                    Title = "Мастер заданий",
                    Description = "Выполни 50 квестов",
                    IsUnlocked = false,
                    Progress = 23,
                    MaxProgress = 50
                }
            },
            {
                "ArmorCollector",
                new Achievement
                {
                    Title = "Бронированный",
                    Description = "Собери все виды брони",
                    IsUnlocked = false,
                    Progress = 3,
                    MaxProgress = 10
                }
            },
            {
                "MagicMaster",
                new Achievement
                {
                    Title = "Мастер магии",
                    Description = "Изучи 20 заклинаний",
                    IsUnlocked = false,
                    Progress = 8,
                    MaxProgress = 20
                }
            },
            {
                "Survivor",
                new Achievement
                {
                    Title = "Выживший",
                    Description = "Выживи 30 дней",
                    IsUnlocked = true,
                    UnlockDate = new DateTime(2024, 2, 1),
                    Progress = 30,
                    MaxProgress = 30
                }
            },
            {
                "Fisherman",
                new Achievement
                {
                    Title = "Рыбак",
                    Description = "Поймай 50 рыб",
                    IsUnlocked = false,
                    Progress = 12,
                    MaxProgress = 50
                }
            }
        }
};
byte[] data;

// Сериализация
using (var stream = new MemoryStream())
{
    using (var serializer = new ByteSerialeze(stream))
    {
        serializer.Serialize(player);
    }
    data = stream.ToArray();
}
File.Delete("player_save.dat");
File.Delete("player_save_debug.txt");
// Сохранение в файл
File.WriteAllBytes("player_save.dat", data);

// читаемый вывод в текстовый файл для анализа!
using (var writer = new StreamWriter("player_save_debug.txt"))
{
    writer.WriteLine("=== Binary Data Debug View ===");
    writer.WriteLine($"Total bytes: {data.Length}\n");

    // Вывод в hex формате с ASCII представлением
    for (int i = 0; i < data.Length; i += 16)
    {
        // Пишем смещение
        writer.Write($"{i:X8}: ");

        // Пишем hex представление
        for (int j = 0; j < 16; j++)
        {
            if (i + j < data.Length)
                writer.Write($"{data[i + j]:X2} ");
            else
                writer.Write("   ");

            if (j == 7) writer.Write(" "); // Дополнительный пробел для читаемости
        }

        // Пишем ASCII представление
        writer.Write(" | ");
        for (int j = 0; j < 16 && i + j < data.Length; j++)
        {
            char c = (char)data[i + j];
            writer.Write(char.IsControl(c) ? '.' : c);
        }

        writer.WriteLine();
    }
}

// Десериализация
using (var stream = new MemoryStream(data))
{ 
    using (var deserializer = new ByteDeserializer(stream))
    {
        var loadedData = deserializer.Deserialize<PlayerData>();
        Console.WriteLine($"{loadedData}");
    }
}
Console.Read();
