using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;

namespace SerializatorFox
{
    internal class ApplicationData
    {
        private static Dictionary<Assembly, ApplicationData> AssemblysInstance;

        private readonly Dictionary<Type, byte[]> typeHashes;
        private readonly Dictionary<byte[], Type> typesByHash;

        private readonly Dictionary<string, byte[]> fieldsHashes;

        private readonly Dictionary<byte[], string> fieldsByHash;

        private static readonly object _lock = new object();

        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y) => x.SequenceEqual(y);
            public int GetHashCode(byte[] obj)
            {
                {
                    if (obj == null)
                        return 0;

                    unchecked
                    {
                        int hash = 17;
                        foreach (byte b in obj)
                        {
                            hash = hash * 31 + b;
                        }
                        return hash;
                    }
                }
            }
        }

        private ApplicationData(Assembly assemblyCurrent)
        {
            typeHashes = new Dictionary<Type, byte[]>();
            fieldsHashes = new Dictionary<string, byte[]>();
            typesByHash = new Dictionary<byte[], Type>(new ByteArrayComparer());
            fieldsByHash = new Dictionary<byte[], string>(new ByteArrayComparer());

            foreach (Type type in assemblyCurrent.GetTypes().
                Where(type => type.GetCustomAttribute<SerializableTypeAttribute>() != null))
            {
                RegisterType(type);
            }
        }
        public void RegisterType(Type type, bool depth = false)
        {
            if (typeHashes.ContainsKey(type) || type.FullName == null)
                return;

            var hash = ComputeHash(type.FullName);

            if (typesByHash.ContainsKey(hash))
            {
                Console.WriteLine($"Коллизия! {type.FullName} Hash - {hash}");
            }

            typeHashes[type] = hash;
            typesByHash[hash] = type;

            // Обработка полей
            foreach (var field in GetSerializableFields(type))
            {
                RegisterField(field);
                ProcessFieldType(field, depth);
            }
        }
        private void RegisterField(FieldInfo field)
        {
            string fieldName = field.Name;

            // Если поле уже зарегистрировано - пропускаем
            if (fieldsHashes.ContainsKey(fieldName))
                return;

            var fieldHash = ComputeHash(fieldName);

            // Проверяем коллизии
            if (fieldsByHash.ContainsKey(fieldHash))
            {
                Console.WriteLine($"Коллизия! {fieldName} Hash - {fieldHash}");
                return;
            }
            // Регистрируем хеши
            fieldsHashes[fieldName] = fieldHash;
            fieldsByHash[fieldHash] = fieldName;
        }
        public static ApplicationData Get()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            lock (_lock)
            {
                if (AssemblysInstance == null)
                {
                    AssemblysInstance = new Dictionary<Assembly, ApplicationData>();
                }
                if (!AssemblysInstance.ContainsKey(assembly))
                {
                    AssemblysInstance.Add(assembly, new ApplicationData(assembly));
                }
                return AssemblysInstance[assembly];
            }
        }

        public byte[] GetTypeHash(Type type)
        {
            if (!typeHashes.TryGetValue(type, out byte[] hash))
            {
                throw new CustomAttributeFormatException(
                    $"Type {type.FullName} is not marked with [SerializableType] attribute");
            }
            return hash;
        }

        public Type GetTypeByHash(byte[] hash)
        {
            if (hash == null || hash.Length == 0)
            {
                throw new ArgumentException("Hash cannot be null or empty");
            }

            if (!typesByHash.TryGetValue(hash, out Type type))
            {
                throw new CustomAttributeFormatException(
                    $"Hash {BitConverter.ToString(hash)} is not associated with any serializable type");
            }
            return type;
        }

        public byte[] GetPropertyHash(string name)
        {
            if (!fieldsHashes.TryGetValue(name, out byte[] hash))
            {
                throw new CustomAttributeFormatException(
                    $"Property {name} is marked with [IgnoreSerialize] attribute");
            }
            return hash;
        }

        public string GetFieldNameByHash(byte[] hash)
        {
            if (!fieldsByHash.TryGetValue(hash, out string name))
            {
                throw new CustomAttributeFormatException(
                    $"Hash {BitConverter.ToString(hash)} is not associated with any property");
            }
            return name;
        }

        public bool IsFieldSerializable(FieldInfo field) => !IsFieldIgnored(field);

        public bool ShouldSerializeDepth(FieldInfo field) =>
            HasSerializeDepthAttribute(field, out bool depth) && depth;

        public IEnumerable<FieldInfo> GetTypeSerializableFields(Type type) =>
            GetSerializableFields(type);

        private IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            return type.GetRuntimeFields()
                .Where(field => !IsFieldIgnored(field));
        }

        private bool IsFieldIgnored(FieldInfo field)
        {
            return field.GetCustomAttribute<IgnoreSerializeAttribute>() != null;
        }

        private bool HasSerializeDepthAttribute(FieldInfo field, out bool depthValue)
        {
            var attribute = field.GetCustomAttribute<SerializeDepthAttribute>();
            depthValue = attribute?.DeepSerialization ?? false;
            return attribute != null;
        }



        private void ProcessFieldType(FieldInfo field, bool currentDepth)
        {
            Type fieldType = field.FieldType;

            // Пропускаем примитивные типы и строки
            if (fieldType.IsPrimitive || fieldType == typeof(string))
                return;

            // Обрабатываем generic типы
            if (fieldType.IsGenericType)
            {
                foreach (var argType in fieldType.GetGenericArguments())
                {
                    if (!argType.IsPrimitive && argType != typeof(string))
                        RegisterType(argType);
                }
            }

            // Проверяем глубокую сериализацию
            if (HasSerializeDepthAttribute(field, out bool attributeDepthValue) &&
                (fieldType.IsClass || fieldType.IsValueType || fieldType.IsInterface))
            {
                RegisterType(fieldType, currentDepth || attributeDepthValue);
            }
        }
        private byte[] ComputeHash(string input)
        {
            byte[] text = Encoding.UTF8.GetBytes(input);
            return BitConverter.GetBytes(MurmurHash(text));
        }

        public byte[] MD5Hash(string input)
        {
            byte[] text = Encoding.UTF8.GetBytes(input);
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(text);
            }
        }

        public uint MurmurHash(byte[] data, uint seed = 0)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint h1 = seed;
            uint length = (uint)data.Length;
            uint k1;

            // Обработка основных блоков
            for (int i = 0; i < data.Length - 3; i += 4)
            {
                k1 = BitConverter.ToUInt32(data, i);

                k1 *= c1;
                k1 = (k1 << 15) | (k1 >> 17);
                k1 *= c2;

                h1 ^= k1;
                h1 = (h1 << 13) | (h1 >> 19);
                h1 = h1 * 5 + 0xe6546b64;
            }

            // Финализация
            h1 ^= length;
            h1 ^= h1 >> 16;
            h1 *= 0x85ebca6b;
            h1 ^= h1 >> 13;
            h1 *= 0xc2b2ae35;
            h1 ^= h1 >> 16;

            return h1;
        }

        public void CheckData()
        {
            Console.WriteLine("=== Type Hashes ===");
            foreach (var pair in typeHashes)
            {
                Console.WriteLine($"{pair.Key.Name}: {ByteArrayToString(pair.Value)}");
            }

            Console.WriteLine("\n=== Types By Hash ===");
            foreach (var pair in typesByHash)
            {
                Console.WriteLine($"{ByteArrayToString(pair.Key)}: {pair.Value.Name}");
            }

            Console.WriteLine("\n=== Property Hashes ===");
            foreach (var pair in fieldsHashes)
            {
                Console.WriteLine($"{pair.Key}: {ByteArrayToString(pair.Value)}");
            }

            Console.WriteLine("=== Size Comparison ===");
            foreach (var pair in fieldsHashes) // Берём первые 5 для примера
            {
                string name = pair.Key;
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                byte[] hashBytes = pair.Value;

                Console.WriteLine($"Property: {name}");
                Console.WriteLine($"Name bytes ({nameBytes.Length}): {BitConverter.ToString(nameBytes).Replace("-", "")}");
                Console.WriteLine($"Hash bytes ({hashBytes.Length}): {BitConverter.ToString(hashBytes).Replace("-", "")}");
                Console.WriteLine();
            }
        }
        private string ByteArrayToString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

    }
}
