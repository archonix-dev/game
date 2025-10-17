using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Material manager */
public class ObjectMaterialManager : MonoSingleton<ObjectMaterialManager>
{
    MaterialDefinitions materialData;
    private bool isInitialized = false;

    /* Pull material data from json on startup */
    private void Start()
    {
        try
        {
            string jsonPath = Application.streamingAssetsPath + "/material_types.json";
            if (System.IO.File.Exists(jsonPath))
            {
                string jsonContent = System.IO.File.ReadAllText(jsonPath);
                materialData = JsonUtility.FromJson<MaterialDefinitions>(jsonContent);
                isInitialized = true;
                Debug.Log("ObjectMaterialManager: Successfully loaded material data from JSON");
            }
            else
            {
                Debug.LogWarning("ObjectMaterialManager: material_types.json not found at " + jsonPath + ". Using default values.");
                InitializeDefaultMaterials();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("ObjectMaterialManager: Failed to load material data: " + e.Message + ". Using default values.");
            InitializeDefaultMaterials();
        }
    }

    /* Initialize default materials if JSON is not available */
    private void InitializeDefaultMaterials()
    {
        materialData = new MaterialDefinitions();
        materialData.materials = new List<MaterialDefinition>
        {
            new MaterialDefinition { type = "BEDROCK", can_shatter = false, strength = 999, density = 100 },
            new MaterialDefinition { type = "GLASS", can_shatter = true, strength = 10, density = 20 },
            new MaterialDefinition { type = "CONCRETE", can_shatter = true, strength = 50, density = 80 }
        };
        isInitialized = true;
        Debug.Log("ObjectMaterialManager: Initialized with default material values");
    }

    /* Can the requested material break? */
    public bool CanMaterialBreak(MaterialTypes type)
    {
        if (!isInitialized || materialData == null || materialData.materials == null)
        {
            Debug.LogWarning("ObjectMaterialManager: Not initialized properly. Returning default value.");
            return true; // По умолчанию разрешаем разрушение
        }

        foreach (MaterialDefinition material in materialData.materials)
        {
            if (material.type == type.ToString())
            {
                return material.can_shatter;
            }
        }
        Debug.LogWarning("Tried to access data for non-existant material (" + type + "). Returning default value.");
        return true; // По умолчанию разрешаем разрушение
    }

    /* What is the strength of the requested material? */
    public int GetMaterialStrength(MaterialTypes type)
    {
        if (!isInitialized || materialData == null || materialData.materials == null)
        {
            Debug.LogWarning("ObjectMaterialManager: Not initialized properly. Returning default strength.");
            return 30; // Значение по умолчанию
        }

        foreach (MaterialDefinition material in materialData.materials)
        {
            if (material.type == type.ToString())
            {
                if (!material.can_shatter)
                {
                    Debug.LogWarning("Tried to access strength value of non-breakable material (" + type + "). Always call CanMaterialBreak first!");
                    return 999;
                }
                return material.strength;
            }
        }
        Debug.LogWarning("Tried to access data for non-existant material (" + type + "). Returning default strength.");
        return 30; // Значение по умолчанию
    }

    /* What is the density of the requested material? */
    public int GetMaterialDensity(MaterialTypes type)
    {
        if (!isInitialized || materialData == null || materialData.materials == null)
        {
            Debug.LogWarning("ObjectMaterialManager: Not initialized properly. Returning default density.");
            return 50; // Значение по умолчанию
        }

        foreach (MaterialDefinition material in materialData.materials)
        {
            if (material.type == type.ToString())
            {
                if (!material.can_shatter)
                {
                    Debug.LogWarning("Tried to access density value of non-breakable material (" + type + "). Always call CanMaterialBreak first!");
                    return 100;
                }
                return material.density;
            }
        }
        Debug.LogWarning("Tried to access data for non-existant material (" + type + "). Returning default density.");
        return 50; // Значение по умолчанию
    }
}

/* Material json setup */
[System.Serializable]
public class MaterialDefinitions
{
    public List<MaterialDefinition> materials;
}
[System.Serializable]
public class MaterialDefinition
{
    public string type;      //The name of the material
    public bool can_shatter; //If this material is breakable or not
    public int strength;     //The strength of this material (how hard does it need to be hit/dropped to break)
    public int density;      //The density of this material (how much it should shatter when broken)
}

/* This enum is auto-populated... do not edit anything below this line! */
public enum MaterialTypes
{
    /*START*/
    BEDROCK,
    GLASS,
    CONCRETE,
    /*END*/
}
