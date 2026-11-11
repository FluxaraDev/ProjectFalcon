using System;
using System.Collections.Generic;

[Serializable]
public class PathGroup
{
	public string path;

	public List<PathGroup> subGroups = new List<PathGroup>();
}
