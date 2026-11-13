namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 标签的用途。触因 / 人物 / 课题都复用了整条标签管线，只是多了一个分类维度。
    /// <para>
    /// 原先还预留了 <c>Category</c>（本想走「日课点选类别」那条路），但 2026-10-05 定成
    /// 「归类是周课的人工动作、类别是自由文本」之后，再没有任何东西会写它，于是删掉——
    /// 留着只会让人以为还有一条走得通的路。
    /// </para>
    /// </summary>
    public enum TagKind
    {
        Normal = 0,
        Trigger = 1,

        /// <summary>人物。挂在日记上，点这个标签就能看到和这个人的全部记录。</summary>
        Person = 2,

        /// <summary>课题。和人物分开，这样「谁」和「哪类功课」是两个维度，不是一个标签兼两用。</summary>
        Lesson = 3,
    }
}
