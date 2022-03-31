
using System.Collections.Generic;
using Unity.Entities;
using UnityGameObject = UnityEngine.GameObject;
using UnityRangeAttribute = UnityEngine.RangeAttribute;
using UnityMonoBehaviour = UnityEngine.MonoBehaviour;

public class AdhocResourceSpawnerAuthoring : UnityMonoBehaviour
	, IConvertGameObjectToEntity
	, IDeclareReferencedPrefabs
{
	public UnityGameObject ResourcePrefab;
	[UnityRangeAttribute(0, 1000)] public int ResourceCount = 1;


	void Awake()
	{
	//	EntityManager em = World.DefaultGameObjectInjectionWorld.EntityManager;
	//	Entity adhocResource = em.CreateEntity();
	//	Convert(adhocResource, em, );
	}

	// This function is required by IDeclareReferencedPrefabs
	public void DeclareReferencedPrefabs(List<UnityGameObject> referencedPrefabs)
	{
		// Conversion only converts the GameObjects in the scene.
		// This function allows us to inject extra GameObjects,
		// in this case prefabs that live in the assets folder.
		referencedPrefabs.Add(ResourcePrefab);
	}

	// This function is required by IConvertGameObjectToEntity
	public void Convert(Entity entity, EntityManager dstManager
		, GameObjectConversionSystem conversionSystem)
	{
		// GetPrimaryEntity fetches the entity that resulted from the conversion of
		// the given GameObject, but of course this GameObject needs to be part of
		// the conversion, that's why DeclareReferencedPrefabs is important here.
		dstManager.AddComponentData(entity, new AdhocResourceSpawnerComponent
		{
			ResourcePrefab = conversionSystem.GetPrimaryEntity(ResourcePrefab),
			ResourceCount = ResourceCount,
			ResourceSpawnPosition = this.gameObject.transform.position
		});
	}
}