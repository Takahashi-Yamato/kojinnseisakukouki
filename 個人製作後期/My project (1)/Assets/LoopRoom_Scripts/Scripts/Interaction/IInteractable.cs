/// <summary>
/// 調べられるもの全般。ドア、ベッド、今後追加するメモや引き出しなど。
/// </summary>
public interface IInteractable
{
    /// <summary>画面中央に出すヒント文（例：「ドアを開ける」）</summary>
    string Prompt { get; }

    void Interact();
}
