using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One page of story content for an exhibit card.
/// </summary>
[System.Serializable]
public class ExhibitStoryPage
{
    [TextArea(3, 8)]
    public string body;
}

/// <summary>
/// ScriptableObject containing all story data for a single exhibit animal.
/// Create one asset per animal via Assets > Create > VR Exhibit > Story.
/// </summary>
[CreateAssetMenu(fileName = "ExhibitStory", menuName = "VR Exhibit/Story")]
public class ExhibitStory : ScriptableObject
{
    public string animalName;
    public string scientificName;
    public string status = "EXTINCT";

    [Space]
    public List<ExhibitStoryPage> pages;
}
