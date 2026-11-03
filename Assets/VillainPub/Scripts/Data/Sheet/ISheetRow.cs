namespace VillainPub.Data
{
    /// <summary>
    /// 구글 시트 한 행에 대응하는 데이터 클래스. 다른 데이터는 이 Id로 참조한다.
    /// </summary>
    public interface ISheetRow
    {
        string Id { get; }
    }
}
