﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using CleanKludge.Core.Articles;
using CleanKludge.Data.File.Serializers;
using Microsoft.Extensions.Caching.Memory;

namespace CleanKludge.Data.File.Articles
{
    public class ArticleSummaryRepository : IArticleSummaryRepository
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IArticlePath _articlePath;
        private readonly ISerializer _serializer;

        public ArticleSummaryRepository(IArticlePath articlePath, IMemoryCache memoryCache, ISerializer serializer)
        {
            _articlePath = articlePath;
            _memoryCache = memoryCache;
            _serializer = serializer;
        }

        public IArticleSummaryDto FetchOne(ArticleIdentifier identifier)
        {
            if(_memoryCache.TryGetValue(MemoryCacheKey.ForSummary(identifier), out ArticleSummaryRecord articleRecord))
                return articleRecord;

            var data = _articlePath.LoadFor(identifier);
            articleRecord = _serializer.Deserialize<ArticleSummaryRecord>(data);
            return _memoryCache.Set(MemoryCacheKey.ForSummary(identifier), articleRecord);
        }

        public IList<IArticleSummaryDto> FetchAll()
        {
            if(_memoryCache.TryGetValue(MemoryCacheKey.Summaries(), out IList<IArticleSummaryDto> records))
                return records;

            records = new List<IArticleSummaryDto>();
            foreach (var path in _articlePath.GetAll())
            {
                var fileInfo = new FileInfo(path);
                var articleIdentifier = ArticleIdentifier.From(fileInfo.Name);
                if(!_memoryCache.TryGetValue(MemoryCacheKey.ForSummary(articleIdentifier), out ArticleSummaryRecord summaryRecord))
                {
                    summaryRecord = _serializer.Deserialize<ArticleSummaryRecord>(_articlePath.LoadFrom(path));
                    _memoryCache.Set(MemoryCacheKey.ForSummary(summaryRecord.Identifier), summaryRecord);
                }

                if(summaryRecord.Enabled)
                    records.Add(summaryRecord);
            }

            return _memoryCache
                .Set(MemoryCacheKey.Summaries(), records)
                .ToList();
        }
    }
}