using System;
using System.Collections.Generic;
using Google.Maps;
using Google.Maps.Feature;
using Google.Maps.Feature.Style;
using UnityEngine;

/// <summary>
/// Prefab replacement example, demonstrating how to use Maps Unity SDK's events to replace specific
/// buildings.
/// </summary>
/// <remarks>
/// Shows how to use a defined prefab to replace <see cref="ExtrudedStructure"/>s and
/// <see cref="ModeledStructure"/>s of given <see cref="StructureMetadata.UsageType"/>s; or
/// <see cref="ModeledStructure"/>s that were suppressed by the SDK because they have a vertex count
/// that exceeds Unity's maximum support vertex count (65,000 vertices per mesh).
/// <para>
/// This scene starts focused on Westminster Abbey (London), as this model is currently too
/// detailed for Unity to handle, and so is suppressed.
/// </para>
/// This script uses <see cref="DynamicMapsService"/> to allow navigation around the world, with the
/// <see cref="Google.Maps.MapsService"/> keeping only the viewed part of the world loaded at all
/// times.
/// <para>
/// This script also uses <see cref="ErrorHandling"/> component to display any errors encountered by
/// the <see cref="MapsService"/> component when loading geometry.
/// </para></remarks>
[RequireComponent(typeof(DynamicMapsService), typeof(ErrorHandling))]
public sealed class PrefabReplacementModified : MonoBehaviour {

    Dictionary<string, ExtrudedStructureStyle> extrudedStructureStyles = new Dictionary<string, ExtrudedStructureStyle>();
    Dictionary<string, ModeledStructureStyle> modeledStructureStyles = new Dictionary<string, ModeledStructureStyle>();
    ExtrudedStructureStyle extrudedPrefabStyle;
    ModeledStructureStyle modeledPrefabStyle;

    [Header("Prefabs") ,Tooltip("Prefab to use to replace buildings.")]
    public GameObject Prefab;

    [Tooltip("Custom Place.")]
    [SerializeField] String CustomLocation;

    [Tooltip("Prefab to use to replace Custom Place.")]
    [SerializeField] GameObject PrefabCustom;

    [Tooltip("Prefab to use to replace Bars.")]
    [SerializeField] GameObject PrefabBar;

    [Tooltip("Prefab to use to replace Banks.")]
    [SerializeField] GameObject PrefabBank;

    [Tooltip("Prefab to use to replace Lodgings.")]
    [SerializeField] GameObject PrefabLodging;

    [Tooltip("Prefab to use to replace Cafes.")]
    [SerializeField] GameObject PrefabCafe;

    [Tooltip("Prefab to use to replace Restaurants.")]
    [SerializeField] GameObject PrefabRestaurant;

    [Tooltip("Prefab to use to replace Event Venues.")]
    [SerializeField] GameObject PrefabEvent;

    [Tooltip("Prefab to use to replace Tourist Destinations.")]
    [SerializeField] GameObject PrefabTourist;

    [Tooltip("Prefab to use to replace Shops.")]
    [SerializeField] GameObject PrefabShop;

    [Tooltip("Prefab to use to replace Schools.")]
    [SerializeField] GameObject PrefabSchool;

    [Tooltip("Prefab to use to replace Unspecified Locations.")]
    [SerializeField] GameObject PrefabUnspecified;

    [Tooltip("Prefab to use to replace Suppressed Locations.")]
    [SerializeField] GameObject PrefabSuppressed;

  [Header("Options"), Tooltip("Replace bars with a prefab?")]
  public bool ReplaceBars;

  [Tooltip("Replace banks with a prefab?")]
  public bool ReplaceBanks = true;

  [Tooltip("Replace lodgings with a prefab?")]
  public bool ReplaceLodgings;

  [Tooltip("Replace cafes with a prefab?")]
  public bool ReplaceCafes;

  [Tooltip("Replace restaurants with a prefab?")]
  public bool ReplaceRestaurants;

  [Tooltip("Replace event venues with a prefab?")]
  public bool ReplaceEventVenues;

  [Tooltip("Replace tourist destinations with a prefab?")]
  public bool ReplaceTouristDestinations;

  [Tooltip("Replace shops with a prefab?")]
  public bool ReplaceShops;

  [Tooltip("Replace schools with a prefab?")]
  public bool ReplaceSchools;

  [Tooltip("Replace buildings without a specified usage type with a prefab?")]
  public bool ReplaceUnspecifieds;

  [Tooltip("Replace very high poly buildings with this prefab? Note that Unity is unable "
      + "to display meshes with over 65,000 vertices, so buildings with more vertices are replaced "
      + "with null meshes. If this toggle is enabled, these buildings will be replaced with a "
      + "prefab instead.")]
  public bool ReplaceSuppressed = true;

  /// <summary>
  /// Use <see cref="DynamicMapsService"/> to load geometry, replacing any buildings as needed.
  /// </summary>
  private void Awake() {
    // Make sure a prefab has been specified.
    if (Prefab == null) {
      Debug.LogError(ExampleErrors.MissingParameter(this, Prefab, "Prefab",
          "to replace specific buildings with"));
      return;
    }

    // Get required DynamicMapsService component on this GameObject.
    DynamicMapsService dynamicMapsService = GetComponent<DynamicMapsService>();

    // See if any options have been set indicating which types of buildings to replace, signing up
    // to WillCreate events if so.
    if (ReplaceBars || ReplaceBanks || ReplaceLodgings || ReplaceCafes || ReplaceRestaurants
        || ReplaceEventVenues || ReplaceTouristDestinations || ReplaceShops || ReplaceSchools
        || ReplaceUnspecifieds) {

        // Create styles for ExtrudedStructure and ModeledStructure type buildings that are to be
        // replaced with a prefab.
        createStyles();

        // Sign up to events called just before any new building is loaded, so we can check each
        // building's usage type and replace it with prefab if needed. Note that:
        // - DynamicMapsService.MapsService is auto-found on first access (so will not be null).
        // - These events must be set now during Awake, so that when DynamicMapsService starts
        //   loading the map during Start, these event will be triggered for all ExtrudedStructures
        //   and ModeledStructures.
      dynamicMapsService.MapsService.Events.ExtrudedStructureEvents.WillCreate.AddListener(args => {
        StructureMetadata.UsageType usage = args.MapFeature.Metadata.Usage;
        if (ShouldReplaceBuilding(usage)) {
          args.Style = extrudedStructureStyles.ContainsKey(usage.ToString()) ? extrudedStructureStyles[usage.ToString()] : extrudedPrefabStyle;
        }
        if (args.MapFeature.Metadata.PlaceId == CustomLocation)
        {
              args.Style = extrudedStructureStyles["Custom"];
        }
      });
      dynamicMapsService.MapsService.Events.ModeledStructureEvents.WillCreate.AddListener(args => {
        StructureMetadata.UsageType usage = args.MapFeature.Metadata.Usage;
        if (ShouldReplaceBuilding(usage)) {
              args.Style = modeledStructureStyles.ContainsKey(usage.ToString()) ? modeledStructureStyles[usage.ToString()] : modeledPrefabStyle;
        }
        if (args.MapFeature.Metadata.PlaceId == CustomLocation)
        {
            args.Style = modeledStructureStyles["Custom"];
        }
      });
    }

    // See if we should be replacing any suppressed buildings with prefab, signing up to DidCreate
    // event if so.
    if (ReplaceSuppressed) {
      // Sign up to event called just after any new building is loaded, so we can check if the
      // building's mesh has been suppressed and should be replaced with a prefab.
      dynamicMapsService.MapsService.Events.ExtrudedStructureEvents.DidCreate.AddListener(
          args => TryReplaceSuppressedBuilding(args.GameObject));
      dynamicMapsService.MapsService.Events.ModeledStructureEvents.DidCreate.AddListener(
          args => TryReplaceSuppressedBuilding(args.GameObject));
    }
  }

    private void createStyles()
    {
        Prefab.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabBar.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabBank.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabLodging.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabCafe.transform.localScale = new Vector3(6f, 6f, 6f);
        PrefabRestaurant.transform.localScale = new Vector3(6f, 6f, 6f);
        PrefabEvent.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabTourist.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabShop.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabSchool.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabUnspecified.transform.localScale = new Vector3(4f, 4f, 4f);
        PrefabSuppressed.transform.localScale = new Vector3(4f, 4f, 4f);


        extrudedPrefabStyle = new ExtrudedStructureStyle.Builder
        {
            Prefab = Prefab
        }.Build();
        modeledPrefabStyle = new ModeledStructureStyle.Builder
        {
            Prefab = Prefab
        }.Build();


        extrudedStructureStyles.Add("Custom", new ExtrudedStructureStyle.Builder { Prefab = PrefabCustom }.Build());
        modeledStructureStyles.Add("Custom", new ModeledStructureStyle.Builder { Prefab = PrefabCustom }.Build());
       

        extrudedStructureStyles.Add("Bar", new ExtrudedStructureStyle.Builder { Prefab = PrefabBar }.Build());
        modeledStructureStyles.Add("Bar", new ModeledStructureStyle.Builder { Prefab = PrefabBar }.Build());

        extrudedStructureStyles.Add("Bank", new ExtrudedStructureStyle.Builder { Prefab = PrefabBank }.Build());
        modeledStructureStyles.Add("Bank", new ModeledStructureStyle.Builder { Prefab = PrefabBank }.Build());

        extrudedStructureStyles.Add("Lodging", new ExtrudedStructureStyle.Builder { Prefab = PrefabLodging }.Build());
        modeledStructureStyles.Add("Lodging", new ModeledStructureStyle.Builder { Prefab = PrefabLodging }.Build());
 
        extrudedStructureStyles.Add("Cafe", new ExtrudedStructureStyle.Builder { Prefab = PrefabCafe }.Build());
        modeledStructureStyles.Add("Cafe", new ModeledStructureStyle.Builder { Prefab = PrefabCafe }.Build());
  
        extrudedStructureStyles.Add("Restaurant", new ExtrudedStructureStyle.Builder { Prefab = PrefabRestaurant }.Build());
        modeledStructureStyles.Add("Restaurant", new ModeledStructureStyle.Builder { Prefab = PrefabRestaurant }.Build());

        extrudedStructureStyles.Add("Event", new ExtrudedStructureStyle.Builder { Prefab = PrefabEvent }.Build());
        modeledStructureStyles.Add("Event", new ModeledStructureStyle.Builder { Prefab = PrefabEvent }.Build());
 
        extrudedStructureStyles.Add("Tourist", new ExtrudedStructureStyle.Builder { Prefab = PrefabTourist }.Build());
        modeledStructureStyles.Add("Tourist", new ModeledStructureStyle.Builder { Prefab = PrefabTourist }.Build());
 
        extrudedStructureStyles.Add("Shop", new ExtrudedStructureStyle.Builder { Prefab = PrefabShop }.Build());
        modeledStructureStyles.Add("Shop", new ModeledStructureStyle.Builder { Prefab = PrefabShop }.Build());

        extrudedStructureStyles.Add("School", new ExtrudedStructureStyle.Builder { Prefab = PrefabSchool }.Build());
        modeledStructureStyles.Add("School", new ModeledStructureStyle.Builder { Prefab = PrefabSchool }.Build());
 
        extrudedStructureStyles.Add("Unspecified", new ExtrudedStructureStyle.Builder { Prefab = PrefabUnspecified }.Build());
        modeledStructureStyles.Add("Unspecified", new ModeledStructureStyle.Builder { Prefab = PrefabUnspecified }.Build());

        extrudedStructureStyles.Add("Suppressed", new ExtrudedStructureStyle.Builder { Prefab = PrefabSuppressed }.Build());
        modeledStructureStyles.Add("Suppressed", new ModeledStructureStyle.Builder { Prefab = PrefabSuppressed }.Build());

    }

    /// <summary>
    /// Check if a building of a given <see cref="StructureMetadata.UsageType"/> should be replaced
    /// with a prefab.
    /// </summary>
    /// <param name="usage"><see cref="StructureMetadata.UsageType"/> of this building.</param>
    private bool ShouldReplaceBuilding(StructureMetadata.UsageType usage) {
    switch (usage) {
      case StructureMetadata.UsageType.Bar:
        return ReplaceBars;

      case StructureMetadata.UsageType.Bank:
        return ReplaceBanks;

      case StructureMetadata.UsageType.Lodging:
        return ReplaceLodgings;

      case StructureMetadata.UsageType.Cafe:
        return ReplaceCafes;

      case StructureMetadata.UsageType.Restaurant:
        return ReplaceRestaurants;

      case StructureMetadata.UsageType.EventVenue:
        return ReplaceEventVenues;

      case StructureMetadata.UsageType.TouristDestination:
        return ReplaceTouristDestinations;

      case StructureMetadata.UsageType.Shopping:
        return ReplaceShops;

      case StructureMetadata.UsageType.School:
        return ReplaceSchools;

      case StructureMetadata.UsageType.Unspecified:
        return ReplaceUnspecifieds;

      default:
        Debug.LogErrorFormat("{0}.{1} encountered an unhandled {2} of '{3}' for building.\n{0} is "
            + "not yet setup to handle {3} type buildings.",
            name, GetType(), typeof(StructureMetadata.UsageType), usage);
        return false;
    }
  }

  /// <summary>Replace a given building with a prefab if its mesh has been suppressed.</summary>
  /// <param name="building"><see cref="GameObject"/> of this building.</param>
  private void TryReplaceSuppressedBuilding(GameObject building) {
    // Check if this building's geometry has been suppressed by the MapsService. The MapsService
    // suppresses any mesh with over 65,000 vertices, which exceeds Unity's maximum supported vertex
    // count. Suppressed geometry can be detected by checking if the MeshFilter.sharedMesh on a
    // created is null (that is, if the mesh was deliberately not loaded for this building).
    if (building.GetComponent<MeshFilter>() != null)
    {
        if (building.GetComponent<MeshFilter>().sharedMesh != null)
        {
            return;
        }

        // To replace building, we start by hiding the original building we're going to replace. Note
        // that we don't do this by setting it's GameObject to inactive, as this would hide any
        // children, including the prefab we're about to create and make a child of this building.
        // Instead we hide this building by disabling all its MeshRenderers.
        foreach (MeshRenderer meshRenderer in building.GetComponentsInChildren<MeshRenderer>())
        {
            meshRenderer.enabled = false;
        }

        // Created an instance of the prefab we'll use to replace this building's geometry.
        Transform prefabTransform = Instantiate(Prefab).transform;

        // Make this just created prefab instance a child of this building's original GameObject. We do
        // this (instead of just destroying the building's original GameObject), so that if the
        // MapsService wants to remove this building (if it has moved offscreen and needs to be
        // unloaded), the MapsService will still be able to find and remove this building's GameObject,
        // which should also remove the child prefab we're now placing under it.
        prefabTransform.SetParent(building.transform);

        // Move the prefab to the center of the building. Note that only the prefab's x and z
        // coordinates are moved, but its y coordinate (it's height) is maintained. We do this to allow
        // for prefabs that are meant to be hovering off the ground.
        prefabTransform.localPosition = new Vector3(0f, prefabTransform.localPosition.y, 0f);
     }

    
  }
}
