using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;

namespace SerializatorFox
{
    internal class ApplicationData
    {
        public const byte NULL_FLAG = 0b1000_0000;
        public const byte RESERVE_FLAG = 0b0100_0000;
        public const byte COLLISION_MASK = 0b0011_1111;

        private static Dictionary<Assembly, ApplicationData> AssemblysInstance;
        private static readonly object _lock = new object();

        // Хэши типов и полей
        private readonly Dictionary<Type, byte[]> typeHashes = new();
        private readonly Dictionary<byte[], List<Type>> typesByHash;
        private readonly Dictionary<string, byte[]> fieldsHashes = new();
        private readonly Dictionary<byte[], List<string>> fieldsByHash;

        // ── Кэши рефлексии (пункты 3 и 4) ──────────────────────────
        // Закэшированные сериализуемые поля по типу (без IgnoreSerialize)
        private readonly Dictionary<Type, FieldInfo[]> cachedSerializableFields = new();

        // Флаг: нужно ли игнорировать поле (результат GetCustomAttribute закэширован)
        private readonly Dictionary<FieldInfo, bool> fieldIgnoreCache = new();
        // ─────────────────────────────────────────────────────────────

        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y) => x.SequenceEqual(y);
            public int GetHashCode(byte[] obj)
            {
                if (obj == null) return 0;
                unchecked
                {
                    int hash = 17;
                    foreach (byte b in obj) hash = hash * 31 + b;
                    return hash;
                }
            }
        }

        private ApplicationData(Assembly assemblyCurrent)
        {
            typesByHash = new Dictionary<byte[], List<Type>>(new ByteArrayComparer());
            fieldsByHash = new Dictionary<byte[], List<string>>(new ByteArrayComparer());

            foreach (Type type in assemblyCurrent.GetTypes()
                .Where(t => t.GetCustomAttribute<System.SerializableAttribute>() != null))
            {
                RegisterType(type);
            }
        }

        // ── Регистрация ──────────────────────────────────────────────

        public void RegisterType(Type type, bool depth = false)
        {
            if (typeHashes.ContainsKey(type) || type.FullName == null)
                return;

            var hash = ComputeHash(type.FullName);
            typeHashes[type] = hash;
            SetCollisionType(hash, type);

            // Получаем сериализуемые поля один раз и кэшируем
            var serializableFields = BuildSerializableFields(type);
            cachedSerializableFields[type] = serializableFields;

            foreach (var field in serializableFields)
            {
                RegisterField(field);
                ProcessFieldType(field, depth);
            }
        }

        /// <summary>
        /// Строит и кэширует массив сериализуемых полей типа.
        /// GetCustomAttribute вызывается здесь один раз за поле.
        /// </summary>
        private FieldInfo[] BuildSerializableFields(Type type)
        {
            var fields = type.GetRuntimeFields();
            var result = new List<FieldInfo>();

            foreach (var field in fields)
            {
                // Кэшируем флаг игнора прямо здесь
                bool ignored = field.GetCustomAttribute<IgnoreSerializeAttribute>() != null;
                fieldIgnoreCache[field] = ignored;

                if (!ignored)
                    result.Add(field);
            }

            return result.ToArray();
        }

        // ── Публичные методы доступа ─────────────────────────────────

        public static ApplicationData Get(Assembly assembly = null)
        {
            if (assembly == null)
                assembly = Assembly.GetCallingAssembly();

            lock (_lock)
            {
                AssemblysInstance ??= new Dictionary<Assembly, ApplicationData>();

                if (!AssemblysInstance.ContainsKey(assembly))
                    AssemblysInstance.Add(assembly, new ApplicationData(assembly));

                return AssemblysInstance[assembly];
            }
        }

        /// <summary>
        /// Возвращает закэшированный массив сериализуемых полей — без рефлексии в горячем пути.
        /// </summary>
        public FieldInfo[] GetTypeSerializableFields(Type type)
        {
            if (cachedSerializableFields.TryGetValue(type, out var fields))
                return fields;

            // Тип не был зарегистрирован заранее — регистрируем и возвращаем
            RegisterType(type);
            return cachedSerializableFields[type];
        }

        /// <summary>
        /// Проверка через кэш — GetCustomAttribute не вызывается повторно.
        /// </summary>
        public bool IsFieldSerializable(FieldInfo field)
        {
            if (fieldIgnoreCache.TryGetValue(field, out bool ignored))
                return !ignored;

            // Поле ещё не встречалось — кэшируем на месте
            bool isIgnored = field.GetCustomAttribute<IgnoreSerializeAttribute>() != null;
            fieldIgnoreCache[field] = isIgnored;
            return !isIgnored;
        }

        public bool IsPrimitiveType(Type type)
        {
            return type.IsPrimitive || type == typeof(string) ||
                   type == typeof(DateTime) || type == typeof(TimeSpan) ||
                   type == typeof(Guid) || type == typeof(decimal);
        }

        public bool ShouldSerializeDepth(FieldInfo field) =>
            HasSerializeDepthAttribute(field, out bool depth) && depth;

        public byte[] GetTypeHash(Type type)
        {
            if (!typeHashes.TryGetValue(type, out byte[] hash))
                throw new CustomAttributeFormatException(
                    $"Type {type.FullName} is not marked with [Serializable] attribute");
            return hash;
        }

        public Type GetTypeByHash(byte[] hash, byte slotCollision)
        {
            slotCollision = (byte)(slotCollision & COLLISION_MASK);

            if (hash == null || hash.Length == 0)
                throw new ArgumentException("Hash cannot be null or empty");

            if (!typesByHash.TryGetValue(hash, out List<Type> types))
                throw new CustomAttributeFormatException(
                    $"Hash {BitConverter.ToString(hash)} is not associated with any serializable type");

            if (types.Count > 63)
                throw new OverflowException("Too many collisions for hash!");

            if (types.Count <= slotCollision)
                throw new OverflowException("Going beyond the registered collisions!");

            return types[slotCollision];
        }

        public byte[] GetPropertyHash(string name)
        {
            if (!fieldsHashes.TryGetValue(name, out byte[] hash))
                throw new CustomAttributeFormatException(
                    $"Property {name} is marked with [IgnoreSerialize] attribute");
            return hash;
        }

        public string GetFieldNameByHash(byte[] hash, byte indexCollision)
        {
            if (!fieldsByHash.TryGetValue(hash, out List<string> names))
                return "";

            if (names.Count > 63)
                throw new OverflowException("Too many collisions for hash!");

            if (names.Count <= indexCollision)
                throw new OverflowException("Going beyond the registered collisions!");

            return names[indexCollision];
        }

        public byte GetCollisionIndexType(byte[] hash, Type type)
        {
            if (!typesByHash.ContainsKey(hash))
                throw new NullReferenceException("No type is registered!");
            return (byte)typesByHash[hash].IndexOf(type);
        }

        public byte GetCollisionIndexField(byte[] hash, string nameField)
        {
            if (!fieldsByHash.ContainsKey(hash))
                throw new NullReferenceException("No field is registered!");
            return (byte)fieldsByHash[hash].IndexOf(nameField);
        }

        // ── Приватные вспомогательные методы ────────────────────────

        private void RegisterField(FieldInfo field)
        {
            string fieldName = field.Name;
            if (fieldsHashes.ContainsKey(fieldName)) return;

            var fieldHash = ComputeHash(fieldName);

            if (!fieldsByHash.ContainsKey(fieldHash))
                fieldsByHash.Add(fieldHash, new List<string> { fieldName });
            else
            {
                fieldsByHash[fieldHash].Add(fieldName);
                fieldsByHash[fieldHash].Sort();
            }

            fieldsHashes[fieldName] = fieldHash;
        }

        private void ProcessFieldType(FieldInfo field, bool currentDepth)
        {
            Type fieldType = field.FieldType;

            if (fieldType.IsPrimitive || fieldType == typeof(string))
                return;

            if (fieldType.IsGenericType)
            {
                foreach (var argType in fieldType.GetGenericArguments())
                {
                    if (!argType.IsPrimitive && argType != typeof(string))
                        RegisterType(argType);
                }
            }

            if (HasSerializeDepthAttribute(field, out bool attributeDepthValue) &&
                (fieldType.IsClass || fieldType.IsValueType || fieldType.IsInterface))
            {
                RegisterType(fieldType, currentDepth || attributeDepthValue);
            }
        }

        private void SetCollisionType(byte[] hash, Type type)
        {
            if (!typesByHash.ContainsKey(hash))
            {
                typesByHash.Add(hash, new List<Type> { type });
                return;
            }

            if (typesByHash[hash].Count + 1 > 63)
                throw new OverflowException("Too many collisions for hash!");

            if (typesByHash[hash].Contains(type)) return;

            typesByHash[hash].Add(type);
            typesByHash[hash].Sort();
        }

        private bool HasSerializeDepthAttribute(FieldInfo field, out bool depthValue)
        {
            var attribute = field.GetCustomAttribute<SerializeDepthAttribute>();
            depthValue = attribute?.DeepSerialization ?? false;
            return attribute != null;
        }

        private byte[] ComputeHash(string input)
        {
            byte[] text = Encoding.UTF8.GetBytes(input);
            return BitConverter.GetBytes(MurmurHash(text));
        }

        public uint MurmurHash(byte[] data, uint seed = 0)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint h1 = seed;
            uint length = (uint)data.Length;

            for (int i = 0; i < data.Length - 3; i += 4)
            {
                uint k1 = BitConverter.ToUInt32(data, i);
                k1 *= c1;
                k1 = (k1 << 15) | (k1 >> 17);
                k1 *= c2;
                h1 ^= k1;
                h1 = (h1 << 13) | (h1 >> 19);
                h1 = h1 * 5 + 0xe6546b64;
            }

            h1 ^= length;
            h1 ^= h1 >> 16;
            h1 *= 0x85ebca6b;
            h1 ^= h1 >> 13;
            h1 *= 0xc2b2ae35;
            h1 ^= h1 >> 16;

            return h1;
        }

        public byte[] MD5Hash(string input)
        {
            byte[] text = Encoding.UTF8.GetBytes(input);
            using var md5 = MD5.Create();
            return md5.ComputeHash(text);
        }

        // ── Отладка ──────────────────────────────────────────────────

        public void CheckData()
        {
            Console.WriteLine("=== Type Hashes ===");
            foreach (var pair in typeHashes)
                Console.WriteLine($"{pair.Key.Name}: {ByteArrayToString(pair.Value)}");

            Console.WriteLine("\n=== Types By Hash ===");
            foreach (var pair in typesByHash)
                Console.WriteLine($"{ByteArrayToString(pair.Key)}: {string.Join(", ", pair.Value)}");

            Console.WriteLine("\n=== Property Hashes ===");
            foreach (var pair in fieldsHashes)
                Console.WriteLine($"{pair.Key}: {ByteArrayToString(pair.Value)}");

            Console.WriteLine("\n=== Cached Fields ===");
            foreach (var pair in cachedSerializableFields)
            {
                Console.WriteLine($"{pair.Key.Name}: [{string.Join(", ", pair.Value.Select(f => f.Name))}]");
            }

            Console.WriteLine("\n=== Size Comparison ===");
            foreach (var pair in fieldsHashes)
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

        private string ByteArrayToString(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace("-", "");
    }
}