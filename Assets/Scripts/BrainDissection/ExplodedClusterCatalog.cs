using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines one or more pre-authored clusters of regions that can
/// be displayed in an exploded diagram. Each cluster is a small
/// set (6–12) of anatomically related regions with directional
/// offsets to spread them apart for spatial clarity.
/// </summary>
[CreateAssetMenu(fileName = "ExplodedClusterCatalog", menuName = "Brain Dissection/Exploded Cluster Catalog")]
public class ExplodedClusterCatalog : ScriptableObject
{
    [System.Serializable]
    public struct RegionOffset
    {
        [Tooltip("regionId or GameObject.name")]
        public string regionKey;
        [Tooltip("Direction to push this region in local specimen space")]
        public Vector3 localExplodeDirection;
        [Tooltip("Max distance in meters (scaled by specimen bounds, typically 0.005–0.02)")]
        public float maxOffsetMeters;
    }

    [System.Serializable]
    public struct Cluster
    {
        public string clusterName;
        [TextArea(1, 2)]
        public string description;
        public List<RegionOffset> regions;
    }

    public List<Cluster> clusters = new List<Cluster>();

    /// <summary>
    /// Populate with default basal ganglia + thalamus cluster.
    /// Called if no asset is assigned.
    /// </summary>
    public void PopulateDefaults()
    {
        clusters.Clear();

        // Cluster 1: Basal ganglia circuit
        var basalGanglia = new Cluster
        {
            clusterName = "Basal Ganglia Circuit",
            description = "Caudate, putamen, globus pallidus, nucleus accumbens, subthalamic nucleus, substantia nigra",
            regions = new List<RegionOffset>
            {
                MakeOffset("head_of_caudate_L",                   new Vector3( 0.4f,  0.6f,  0.3f), 0.003f),
                MakeOffset("body_of_caudate_L",                   new Vector3( 0.3f,  0.7f,  0.0f), 0.0025f),
                MakeOffset("putamen_L",                           new Vector3(-0.5f,  0.3f,  0.2f), 0.003f),
                MakeOffset("external_segment_of_globus_pallidus_L", new Vector3(-0.6f, -0.1f, 0.1f), 0.0025f),
                MakeOffset("internal_segment_of_globus_pallidus_L", new Vector3(-0.5f, -0.4f, 0.0f), 0.002f),
                MakeOffset("nucleus_accumbens_L",                 new Vector3( 0.1f, -0.5f,  0.5f), 0.002f),
                MakeOffset("subthalamic_nucleus_L",               new Vector3(-0.3f, -0.7f, -0.2f), 0.0015f),
                MakeOffset("substantia_nigra_L",                  new Vector3(-0.2f, -0.8f, -0.5f), 0.002f),
            }
        };
        clusters.Add(basalGanglia);

        // Cluster 2: Thalamic nuclei neighborhood
        var thalamus = new Cluster
        {
            clusterName = "Thalamic Nuclei",
            description = "Anterior, mediodorsal, pulvinar, ventral lateral, ventral posterior, geniculate nuclei",
            regions = new List<RegionOffset>
            {
                MakeOffset("anterior_nuclear_complex_of_thalamus_L", new Vector3( 0.3f,  0.7f,  0.3f), 0.0025f),
                MakeOffset("mediodorsal_nucleus_of_thalamus_L",      new Vector3( 0.0f,  0.5f,  0.5f), 0.0025f),
                MakeOffset("pulvinar_of_thalamus_L",                 new Vector3(-0.4f,  0.3f, -0.3f), 0.003f),
                MakeOffset("ventral_lateral_nucleus_of_thalamus_L",  new Vector3(-0.3f, -0.2f,  0.4f), 0.002f),
                MakeOffset("ventral_posterior_lateral_nucleus_L",     new Vector3(-0.5f, -0.5f,  0.1f), 0.002f),
                MakeOffset("dorsal_lateral_geniculate_nucleus_L",    new Vector3( 0.4f, -0.4f, -0.5f), 0.0015f),
                MakeOffset("medial_geniculate_nuclei_L",             new Vector3( 0.5f, -0.2f, -0.4f), 0.0015f),
            }
        };
        clusters.Add(thalamus);
    }

    static RegionOffset MakeOffset(string key, Vector3 dir, float dist)
    {
        return new RegionOffset
        {
            regionKey = key,
            localExplodeDirection = dir.normalized,
            maxOffsetMeters = dist
        };
    }
}
