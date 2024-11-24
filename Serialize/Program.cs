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
