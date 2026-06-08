namespace CurseTheBeast.Core.Api.Curseforge;


public class GenericRsp<TModel>
{
    public TModel data { get; init; } = default!;
}
