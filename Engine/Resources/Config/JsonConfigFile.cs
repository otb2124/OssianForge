using System;
using System.Collections.Generic;
using System.Linq;

namespace OssianForge.Engine.Resources.Config
{

    /// <summary>
    /// Base class for all records stored in a <see cref="JsonConfigFile{TRecord}"/>.
    /// The <see cref="Id"/> property acts as the primary key.
    /// </summary>
    public abstract class ConfigRecord
    {
        public string Id { get; set; } = "";

        /// <summary>
        /// Returns the names of all fields (besides Id) that should be persisted,
        /// in the order they appear in the flat key store.
        /// Subclasses override this to drive generic Read/Write without reflection.
        /// </summary>
        public abstract IEnumerable<string> FieldNames { get; }

        /// <summary>Gets a field value by name.</summary>
        public abstract string GetField(string name);

        /// <summary>Sets a field value by name.</summary>
        public abstract void SetField(string name, string value);
    }



    /// <summary>
    /// A <see cref="ConfigFile"/> backed by a JSON root array where every element
    /// is a record with a string <c>id</c> primary key.
    ///
    /// Flat key format (empty prefix because the root is an array):
    ///   "[0].id"        = "some-id"
    ///   "[0].fieldName" = "value"
    ///
    /// Subclasses supply the concrete <typeparamref name="TRecord"/> type and
    /// override <see cref="CreateRecord"/> to hydrate one from the flat store.
    /// </summary>
    public abstract class JsonConfigFile<TRecord> : ConfigFile where TRecord : ConfigRecord, new()
    {
        protected JsonConfigFile(string id, string path)
            : base(id, path, ConfigFormat.Json) { }

        // ── abstract hook ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="ReadRecord"/> after the base fields are populated.
        /// Subclasses can do nothing if <typeparamref name="TRecord"/> only needs Id.
        /// </summary>
        protected virtual TRecord CreateRecord() => new TRecord();

        // ── index helpers ────────────────────────────────────────────────────────

        /// <summary>Returns the highest N found in keys of the form [N].*, or -1.</summary>
        protected int GetLastIndex()
        {
            int max = -1;
            foreach (var key in GetAll().Keys)
            {
                if (!key.StartsWith('[')) continue;
                int close = key.IndexOf(']');
                if (close > 1 && int.TryParse(key[1..close], out int idx))
                    max = Math.Max(max, idx);
            }
            return max;
        }

        /// <summary>Returns the flat-store index of the record with <paramref name="id"/>, or null.</summary>
        protected int? FindIndex(string id)
        {
            int last = GetLastIndex();
            for (int i = 0; i <= last; i++)
            {
                if (GetString($"[{i}].id") == id)
                    return i;
            }
            return null;
        }

        // ── record I/O ───────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the record at <paramref name="index"/> from the flat store.
        /// Returns null if the slot is empty (id is blank).
        /// </summary>
        protected TRecord? ReadRecord(int index)
        {
            string prefix = $"[{index}]";
            string id = GetString($"{prefix}.id");
            if (string.IsNullOrEmpty(id)) return null;

            var record = CreateRecord();
            record.Id = id;

            foreach (var field in record.FieldNames)
                record.SetField(field, GetString($"{prefix}.{field}"));

            return record;
        }

        /// <summary>Writes all fields of <paramref name="record"/> into the flat store at <paramref name="index"/>.</summary>
        protected void WriteRecord(int index, TRecord record)
        {
            string prefix = $"[{index}]";
            Set($"{prefix}.id", record.Id);

            foreach (var field in record.FieldNames)
                Set($"{prefix}.{field}", record.GetField(field));
        }

        /// <summary>Clears the flat-store slot at <paramref name="index"/>.</summary>
        private void ClearRecord(int index)
        {
            var probe = CreateRecord();
            probe.Id = "";
            string prefix = $"[{index}]";
            Set($"{prefix}.id", "");
            foreach (var field in probe.FieldNames)
                Set($"{prefix}.{field}", "");
        }

        // ── public record API ────────────────────────────────────────────────────

        /// <summary>Returns all records in index order.</summary>
        public List<TRecord> GetAllRecords()
        {
            int last = GetLastIndex();
            var result = new List<TRecord>();
            for (int i = 0; i <= last; i++)
            {
                var record = ReadRecord(i);
                if (record != null) result.Add(record);
            }
            return result;
        }

        /// <summary>Returns the record with the given id, or null.</summary>
        public TRecord? GetById(string id)
        {
            int? idx = FindIndex(id);
            return idx.HasValue ? ReadRecord(idx.Value) : null;
        }

        /// <summary>
        /// Returns the record with the given id cast to <typeparamref name="TDerived"/>, or null.
        /// </summary>
        public TDerived? GetById<TDerived>(string id) where TDerived : TRecord
            => GetById(id) as TDerived;

        /// <summary>
        /// Replaces the record identified by <paramref name="id"/> with <paramref name="record"/>.
        /// Returns false if not found.
        /// </summary>
        public bool ReplaceById(string id, TRecord record)
        {
            int? idx = FindIndex(id);
            if (!idx.HasValue)
            {
                Console.WriteLine($"[{GetType().Name}] Record '{id}' not found.");
                return false;
            }
            WriteRecord(idx.Value, record);
            OnRecordReplaced(id, record);
            return true;
        }

        /// <summary>
        /// Appends a new record. Throws if an record with the same id already exists.
        /// </summary>
        public void Add(TRecord record)
        {
            if (FindIndex(record.Id).HasValue)
                throw new InvalidOperationException($"[{GetType().Name}] Record '{record.Id}' already exists.");

            int next = GetLastIndex() + 1;
            WriteRecord(next, record);
            OnRecordAdded(record);
        }

        /// <summary>
        /// Removes the record with the given id, shifting subsequent entries down.
        /// Returns false if not found.
        /// </summary>
        public bool RemoveById(string id)
        {
            int? idx = FindIndex(id);
            if (!idx.HasValue)
            {
                Console.WriteLine($"[{GetType().Name}] Record '{id}' not found.");
                return false;
            }

            int last = GetLastIndex();
            for (int i = idx.Value; i < last; i++)
                WriteRecord(i, ReadRecord(i + 1)!);

            ClearRecord(last);
            OnRecordRemoved(id);
            return true;
        }

        // ── override hooks ───────────────────────────────────────────────────────
        // Subclasses override these to react to mutations (e.g. syncing live instances).

        protected virtual void OnRecordReplaced(string oldId, TRecord newRecord) { }
        protected virtual void OnRecordAdded(TRecord record) { }
        protected virtual void OnRecordRemoved(string id) { }
    }
}