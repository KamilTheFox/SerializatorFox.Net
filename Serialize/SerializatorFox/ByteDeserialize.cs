﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SerializatorFox
{
    public class ByteDeserializer : IDisposable
    {
        private readonly Stream stream;
        private readonly ApplicationData appData;
        private BinaryReader reader;

        public ByteDeserializer(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            reader = new BinaryReader(stream);
            appData = ApplicationData.Get();
        }

        public T Deserialize<T>()
        {
            // Читаем хеш типа
            byte[] typeHash = reader.ReadBytes(4);
            Type actualType = appData.GetTypeByHash(typeHash);

            // Проверяем соответствие типов
            if (actualType != typeof(T))
                throw new TypeAccessException($"Type mismatch. Expected {typeof(T)}, got {actualType}");

            return (T)DeserializeObject(actualType);
        }

        private object DeserializeObject(Type type)
        {
            if (type.IsInterface)
            {
                // Читаем имя реального типа из потока
                byte[] hash = reader.ReadBytes(4);
                // Получаем реальный тип по хешу
                type = appData.GetTypeByHash(hash);

                if (type == null)
                    throw new TypeAccessException($"Cannot find type By Hash {hash}");
            }

            // Создаём экземпляр объекта
            object instance = Activator.CreateInstance(type);

            // Получаем все сериализуемые поля
            foreach (var field in type.GetRuntimeFields())
            {
                if (!appData.IsFieldSerializable(field))
                    continue;

                // Читаем хеш поля
                byte[] fieldHash = reader.ReadBytes(4);
                string fieldName = appData.GetFieldNameByHash(fieldHash);

                // Проверяем соответствие полей
                if (field.Name != fieldName)
                    throw new TypeAccessException($"Field mismatch. Expected {field.Name}, got {fieldName}");

                // Десериализуем значение поля
                object value = DeserializeValue(field.FieldType);
                field.SetValue(instance, value);
            }

            return instance;
        }

        private Array DeserializeArray(Type elementType)
        {
            // Читаем длину массива
            int length = reader.ReadInt32();

            // Создаём массив нужного типа и размера
            Array array = Array.CreateInstance(elementType, length);

            // Десериализуем каждый элемент
            for (int i = 0; i < length; i++)
            {
                object value = DeserializeValue(elementType);
                array.SetValue(value, i);
            }

            return array;
        }

        private object DeserializeValue(Type type)
        {
            // Проверяем на null
            bool isNotNull = reader.ReadBoolean();
            if (!isNotNull)
                return null;

            // Массивы
            if (type.IsArray)
            {
                return DeserializeArray(type.GetElementType());
            }

            // Примитивные типы
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return reader.ReadSByte();
            if (type == typeof(short)) return reader.ReadInt16();
            if (type == typeof(ushort)) return reader.ReadUInt16();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return reader.ReadInt64();
            if (type == typeof(ulong)) return reader.ReadUInt64();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(decimal)) return reader.ReadDecimal();
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(char)) return reader.ReadChar();
            if (type == typeof(string)) return reader.ReadString();
            if (type == typeof(DateTime)) return new DateTime(reader.ReadInt64());
            if (type == typeof(TimeSpan)) return new TimeSpan(reader.ReadInt64());
            if (type == typeof(Guid)) return new Guid(reader.ReadBytes(16));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = type.GetGenericArguments()[0];
                int count = reader.ReadInt32();

                // Создаём список нужного типа
                IList list = (IList)Activator.CreateInstance(type);

                // Десериализуем каждый элемент
                for (int i = 0; i < count; i++)
                {
                    object item = DeserializeValue(elementType);
                    list.Add(item);
                }

                return list;
            }

            // Enum
            if (type.IsEnum)
                return Enum.ToObject(type, reader.ReadInt32());

            // Объекты
            if (type.IsClass || type.IsValueType || type.IsInterface)
            {
                byte[] typeHash = reader.ReadBytes(4);
                Type actualType = appData.GetTypeByHash(typeHash);
                return DeserializeObject(actualType);
            }

            throw new TypeAccessException($"Unsupported type: {type.FullName}");
        }

        public void Dispose()
        {
            reader?.Dispose();
        }
    }
}
