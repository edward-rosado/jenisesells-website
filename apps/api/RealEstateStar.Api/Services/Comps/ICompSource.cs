using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Comps;

public interface ICompSource
{
    string Name { get; }

    Task<List<Comp>> FetchAsync(
        string address, string city, string state, string zip,
        int? beds, int? baths, int? sqft);
}
