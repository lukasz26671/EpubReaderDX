using EpubReader.Application.Models;
using EpubReader.Domain.Entities;

namespace EpubReader.Application.Interfaces;

public interface ISearchService
{
    IReadOnlyList<SearchHit> Search(EpubBook book, string query, int maxResults = 20);
}
