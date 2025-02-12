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
        public const byte NULL_FLAG = 0b1000_0000;
        public const byte RESERVE_FLAG = 0b0100_0000;
        public const byte COLLISION_MASK = 0b0011_1111;

        private static Dictionary<Assembly, ApplicationData> AssemblysInstance;

        private readonly Dictionary<Type, byte[]> typeHashes;

        private readonly Dictionary<byte[], List<Type>> typesByHash;

        private readonly Dictionary<string, byte[]> fieldsHashes;

        private readonly Dictionary<byte[], List<string>> fieldsByHash;

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
            typesByHash = new Dictionary<byte[], List<Type>>(new ByteArrayComparer());
            fieldsByHash = new Dictionary<byte[], List<string>>(new ByteArrayComparer());

            foreach (Type type in assemblyCurrent.GetTypes().
                Where(type => type.GetCustomAttribute<System.SerializableAttribute>() != null))
            {
                RegisterType(type);
            }
        }
        public void RegisterType(Type type, bool depth = false)
        {
            if (typeHashes.ContainsKey(type) || type.FullName == null)
                return;

            var hash = ComputeHash(type.FullName);

            typeHashes[type] = hash;
            SetCollisionType(hash, type);
            // Обработка полей
            foreach (var field in GetSerializableFields(type))
            {
                RegisterField(field);
                ProcessFieldType(field, depth);
            }
        }

        public static ApplicationData Get(Assembly assembly = null)
        {
            if(assembly == null)
                assembly = Assembly.GetCallingAssembly();
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
        public bool IsPrimitiveType(Type type)
        {
            return type.IsPrimitive || type == typeof(string) ||
                   type == typeof(DateTime) || type == typeof(TimeSpan) ||
                   type == typeof(Guid) || type == typeof(decimal);
        }
        public byte[] GetTypeHash(Type type)
        {
            if (!typeHashes.TryGetValue(type, out byte[] hash))
            {
                throw new CustomAttributeFormatException(
                    $"Type {type.FullName} is not marked with [Serializable] attribute");
            }
            return hash;
        }

        public Type GetTypeByHash(byte[] hash, byte slotCollision)
        {
            slotCollision = (byte)(slotCollision & COLLISION_MASK);
            if (hash == null || hash.Length == 0)
            {
                throw new ArgumentException("Hash cannot be null or empty");
            }

            if (!typesByHash.TryGetValue(hash, out List<Type> type))
            {
                throw new CustomAttributeFormatException(
                    $"Hash {BitConverter.ToString(hash)} is not associated with any serializable type");
            }
            if(type.Count > 63)
            {
                throw new OverflowException("Too many collisions for hash!");
            }
            if(type.Count <= slotCollision)
            {
                throw new OverflowException("Going beyond the registered collisions!");
            }
            return type[slotCollision];
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

        public string GetFieldNameByHash(byte[] hash, byte indexCollision)
        {
            if (!fieldsByHash.TryGetValue(hash, out List<string> name))
            {
                return "";
                throw new CustomAttributeFormatException(
                    $"Hash {BitConverter.ToString(hash)} is not associated with any property");
            }
            if (name.Count > 63)
            {
                throw new OverflowException("Too many collisions for hash!");
            }
            if (name.Count <= indexCollision)
            {
                throw new OverflowException("Going beyond the registered collisions!");
            }
            return name[indexCollision];
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

        private void SetCollisionType(byte[] hash, Type type)
        {
            if(!typesByHash.ContainsKey(hash))
            {
                typesByHash.Add(hash, new List<Type>() { type });
            }
            if(typesByHash[hash].Count + 1 > 63)
            {
                throw new OverflowException("Too many collisions for hash!");
            }
            // Если тип уже зарегистрирован - пропускаем
            if (typesByHash[hash].Contains(type)) return;
            typesByHash[hash].Add(type);
            typesByHash[hash].Sort();
        }
        private void RegisterField(FieldInfo field)
        {
            string fieldName = field.Name;

            // Если поле уже зарегистрировано - пропускаем
            if (fieldsHashes.ContainsKey(fieldName))
                return;

            var fieldHash = ComputeHash(fieldName);

            if (!fieldsByHash.ContainsKey(fieldHash))
            {
                fieldsByHash.Add(fieldHash, new List<string>() { fieldName });
            }
            else
            {
                fieldsByHash[fieldHash].Add(fieldName);
                fieldsByHash[fieldHash].Sort();
            }
            // Регистрируем хеш
            fieldsHashes[fieldName] = fieldHash;
            
        }
        public byte GetCollisionIndexType(byte[] hash, Type type)
        {
            if(!typesByHash.ContainsKey(hash))
            {
                throw new NullReferenceException("No type is registered!");
            }
            return (byte)typesByHash[hash].IndexOf(type);
        }
        public byte GetCollisionIndexField(byte[] hash, string nameField)
        {
            if (!fieldsByHash.ContainsKey(hash))
            {
                throw new NullReferenceException("No type is registered!");
            }
            return (byte)fieldsByHash[hash].IndexOf(nameField);
        }
        private static string GenerateRandomString(int length, Random rand)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[rand.Next(s.Length)]).ToArray());
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
                Console.WriteLine($"{ByteArrayToString(pair.Key)}: {string.Join(", ", pair.Value)}");
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
