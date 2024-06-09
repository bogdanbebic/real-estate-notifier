namespace RealEstateNotifier
{
    public record RealEstate(
        string Id,
        string Name,
        string Price,
        string Location,
        string Url,
        int Visited);
}
