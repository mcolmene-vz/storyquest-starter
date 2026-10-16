namespace Game.Core.Data
{
    /// Hands Core the text of one table by name ("classes", "enemies", "levels", "items"; no extension).
    /// Runtime reads Resources, tests read files; Core never touches either.
    public interface ITableSource
    {
        string ReadText(string tableName);
    }
}
