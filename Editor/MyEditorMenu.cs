public static class MyEditorMenu
{
	[Menu("Editor", "Ultimate Throwdown/My Menu Option")]
	public static void OpenMyMenu()
	{
		EditorUtility.DisplayDialog("It worked!", "This is being called from your library's editor code!");
	}
}
