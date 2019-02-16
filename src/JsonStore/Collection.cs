﻿using System;
using JsonStore.Abstractions;
using JsonStore.Serializer;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JsonStore
{
    public abstract class Collection
    {
        protected ISerializer Serializer;
        protected IStoreDocuments DocumentsStore;
        public string Name { get; protected set; }

        public const string IdKey = "_Id";
        public const string DocumentKey = "_Document";
    }

    public abstract class Collection<TDocument, TId, TContent> : Collection, IDocumentUnitOfWork
        where TContent : class
        where TDocument : Document<TContent, TId>, new()
    {

        private readonly Dictionary<TId, DocumentState<TDocument, TId, TContent>> _documentsInScope = new Dictionary<TId, DocumentState<TDocument, TId, TContent>>();

        protected Collection(IStoreDocuments documentsStore, string name = null)
            : this(new JsonSerializer(), documentsStore, name)
        {
        }

        protected Collection(ISerializer serializer, IStoreDocuments documentsStore, string name = null)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Name = GetCollectionName(name);
            DocumentsStore = documentsStore;
        }

        public async Task CommitAsync()
        {
            await DocumentsStore.StoreAsync(this);

            _documentsInScope.Clear();
        }

        public IEnumerable<DocumentState<TDocument, TId, TContent>> GetModifiedDocuments()
        {
            return _documentsInScope
                .Where(d => d.Value.CurrentState != DocumentStates.Unmodified)
                .Select(d => d.Value);
        }

        public IReadOnlyDictionary<string, object> GetIndexedValues(TDocument document, bool ignoreIdKey = false)
        {
            var defaultValues = new Dictionary<string, object>
            {
                [DocumentKey] = Serializer.Serialize(document.Content)
            };

            if (!ignoreIdKey)
            {
                defaultValues.Add(IdKey, document.Id);
            }

            var internalValues = GetIndexedValuesInternal(document);

            if (internalValues != null)
            {
                foreach (var pair in internalValues)
                {
                    defaultValues.Add(pair.Key, pair.Value);
                }
            }

            return defaultValues;
        }

        public TDocument TrackDocumentFromJsonContent(string jsonContent)
        {
            if (jsonContent == null) throw new ArgumentNullException(nameof(jsonContent));

            var content = Serializer.Deserialize<TContent>(jsonContent);

            var document = new TDocument
            {
                Content = content
            };

            if (_documentsInScope.ContainsKey(document.Id))
                throw new InvalidOperationException("This document already exists on the underlying document collection.");

            _documentsInScope.Add(document.Id, new DocumentState<TDocument, TId, TContent>(DocumentStates.Unmodified, document));

            return document;
        }

        public void Add(TDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            if (_documentsInScope.ContainsKey(document.Id))
                throw new InvalidOperationException("This document already exists on the underlying document collection.");

            _documentsInScope.Add(document.Id, new DocumentState<TDocument, TId, TContent>(DocumentStates.Added, document));
        }

        public void MarkAsModified(TId id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));

            var documentToModify = _documentsInScope[id];

            if (documentToModify.CurrentState == DocumentStates.Added)
                return;

            documentToModify.CurrentState = DocumentStates.Modified;
        }

        public void MarkAsRemoved(TId id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));

            var documentToModify = _documentsInScope[id];
            documentToModify.CurrentState = DocumentStates.Removed;
        }

        protected virtual IReadOnlyDictionary<string, object> GetIndexedValuesInternal(TDocument document)
        {
            return null;
        }

        private string GetCollectionName(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                ? typeof(TContent).Name
                : name;
        }
    }

    public abstract class Collection<TDocument, TContent> : Collection<TDocument, string, TContent>
        where TContent : class
        where TDocument : Document<TContent, string>, new()
    {
        protected Collection(IStoreDocuments documentsStore, string name = null) 
            : base(documentsStore, name)
        {
        }

        protected Collection(ISerializer serializer, IStoreDocuments documentsStore, string name = null) 
            : base(serializer, documentsStore, name)
        {
        }
    }
}