// Assets/_Project/Scripts/OblastZero.Gameplay/Props/PropInstanceTag.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Marks an instantiated prop with the template it came from, so
    /// <see cref="GLBPropLoader.ReleaseProp"/> can decrement the right reference count without the
    /// caller having to remember the key.
    ///
    /// <para>Also the signal that distinguishes a real mesh from the primitive fallback: both come back
    /// from <see cref="GLBPropLoader.CreateVisual"/> as a GameObject, and only the mesh path carries
    /// this component. <see cref="ScavengePropDresser"/> relies on that to decide whether hiding the
    /// baked primitive underneath is safe.</para>
    ///
    /// <para>Its own file rather than sharing GLBPropLoader.cs: CLAUDE.md §4 requires file name ==
    /// primary type name, and Unity will not build a MonoScript reference for a MonoBehaviour whose
    /// file does not match its class — which would make this component unserialisable.</para>
    /// </summary>
    public class PropInstanceTag : MonoBehaviour
    {
        [Tooltip("Resources key of the template this instance was cloned from.")]
        public string ResourceKey;
    }
}
