using UnityEngine;
using UnityEditor;
using System.IO;

public class CustomUV : EditorWindow {
    static bool refresh = false;
    bool debug = true;

    #region Atlas settings
    Material AtlasMat;
    int tSize = 64;
    int texel = 4;
    Color bgColor = Color.magenta;
    string atlasName = "TextureAtlas";
    string atlasMatName = "TextureAtlasMat";
    #endregion

    #region Window settings
    int colorMenuWidth = 300;
    int colorSize = 16;
    GUIStyle colorStyle = null;
    GUIStyle editorStyle = null;
    GUIStyle boxStyle = null;
    #endregion

    #region GUI Variables
    Vector2 scrollPos;
    Editor gameObjectEditor;
    Color colorToAdd;
    bool showAdv = false;
    #endregion

    #region Data Holders
	GameObject gameObject;
    Mesh mesh;
    Renderer renderer;
    Texture2D textureAtlas = null;
    Texture2D workTex = null;
    Texture2D[] colors;
    int colorPaletteLength = 0;
    int[] selectedColor;
    int[] prevSelected;
    #endregion






    [MenuItem("Assets/UV Mapper")]
    static void ShowWindow()
    {
        GetWindow<CustomUV>("Simple UV Mapper");
        refresh = true;
    }

	[MenuItem("Assets/UV Mapper", true)]
    static bool Validate()
    {
        if(Selection.activeGameObject == null)
            return false;

        return Selection.activeGameObject.GetComponent<Renderer>() != null;
    }






    void OnGUI()
    {
        //Refreshes everything if needed
        if(!textureAtlas || !workTex || !gameObjectEditor || refresh)
        {
            Refresh();
            refresh = false;
        }

        if(colorStyle == null || editorStyle == null || boxStyle == null)
            CreateStyles();



        GUILayout.BeginHorizontal("Box", GUIStyle.none);



        //Prefab window
        if (gameObject != null)
        {
            gameObjectEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(position.width - colorMenuWidth, position.height), editorStyle);
        }



        GUILayout.BeginVertical();



        GUILayout.Label(gameObject.name, EditorStyles.boldLabel);

        if(gameObject.GetComponent<Renderer>().sharedMaterial != AtlasMat)
            EditorGUILayout.HelpBox("This Game Object isn't using the atlas material.", MessageType.Warning);


        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(2 * position.height / 3));


        //Meshes
        if(mesh)
        {
            for(int i = 0; i < mesh.subMeshCount; i ++)
            {
                prevSelected[i] = selectedColor[i];

                GUILayout.Label("Submesh " + (i + 1), EditorStyles.boldLabel);

                GUILayout.BeginVertical(boxStyle);
                selectedColor[i] = GUILayout.SelectionGrid(selectedColor[i], colors, 16, colorStyle);
                GUILayout.EndVertical();
            }

            if(CheckChanges())
                RemapUV();
        }

        GUILayout.EndScrollView();

        

        //Color Palette
        GUILayout.BeginHorizontal();
        colorToAdd = EditorGUILayout.ColorField(new GUIContent(), colorToAdd, true, false, false, GUILayout.Width(colorMenuWidth/2));

        if(GUILayout.Button("Add Color", GUILayout.Width(colorMenuWidth/2 - 15)))
            SetColor(colorToAdd);

        GUILayout.EndHorizontal();

        if(GUILayout.Button("Auto"))
            AutoUVMap();

        if(GUILayout.Button("Force Refresh"))
            Refresh();

        if(GUILayout.Button("Copy colors"))
            CopyColors();

        showAdv = EditorGUILayout.Foldout(showAdv, "Advanced settings");

        if(showAdv)
        {
            if(GUILayout.Button("Remove Color"))
                RemoveColor();

            if(GUILayout.Button("Reset"))
                ResetTextureAtlas();
        }

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }



    //Saves the texture
    void SaveTex()
    {
        workTex.Apply();
        byte[] bytes = workTex.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/Resources/" + atlasName + ".png", bytes);
        AssetDatabase.Refresh();

        Refresh();
    }



    //Set a color in the next avaiable spot
    void SetColor(Color color)
    {
        if(debug)
            Debug.Log("Adding color " + color);

        color = new Color(color.r, color.g, color.b, 1f);

        for(int i = 0; i < texel; i ++)
            for(int j = 0; j < texel; j ++)
                workTex.SetPixel((j + texel * colorPaletteLength) % textureAtlas.width, i + texel * (int)Mathf.Floor((texel * colorPaletteLength / textureAtlas.width)), color);

        colorPaletteLength ++;

        SaveTex();
    }

    void SetColor(Color[] color)
    {
        for(int z = 0; z < color.Length; z ++)
        {
            if(debug)
                Debug.Log("Adding color " + color[z]);

            color[z] = new Color(color[z].r, color[z].g, color[z].b, 1f);

            for(int i = 0; i < texel; i ++)
                for(int j = 0; j < texel; j ++)
                    workTex.SetPixel((j + texel * colorPaletteLength) % textureAtlas.width, i + texel * (int)Mathf.Floor((texel * colorPaletteLength / textureAtlas.width)), color[z]);

            colorPaletteLength ++;
        }

        SaveTex();
    }


    void RemoveColor()
    {
        if(debug)
            Debug.Log("Removing Color..");

        if(colorPaletteLength == 0)
        {
            if(debug)
                Debug.Log("Couldn't remove the color. Texture Atlas already empty");
        }
        else
        {
            colorPaletteLength --;
            SetColor(Color.magenta);
        }
    }



    //Force Refresh
    void Refresh()
    {
        textureAtlas = Resources.Load(atlasName) as Texture2D;
        AtlasMat = Resources.Load(atlasMatName, typeof(Material)) as Material;


        //Setup for first times
        if(textureAtlas == null || AtlasMat == null)
        {
            if(!Directory.Exists(Application.dataPath + "/Resources"))
            {
                if(debug)
                    Debug.Log("Created Resources Folder");

                AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.Refresh();
            }

            if(!File.Exists(Application.dataPath + "/Resources/" + atlasName + ".png"))
            {
                ResetTextureAtlas();

                if(debug)
                    Debug.Log("Created Texture Atlas");
            }

            TextureImporter textureImporter = (TextureImporter)TextureImporter.GetAtPath("Assets/Resources/" + atlasName + ".png");
            textureImporter.isReadable = true;
            textureImporter.SaveAndReimport();

            if(!File.Exists(Application.dataPath + "/Resources/" + atlasMatName))
            {
                if(debug)
                    Debug.Log("Created the Texture Atlas Material");

                Material mat = new Material(Shader.Find("Standard"));
                mat.mainTexture = Resources.Load(atlasName) as Texture2D;

                AssetDatabase.CreateAsset(mat, "Assets/Resources/" + atlasMatName + ".mat");
                AssetDatabase.Refresh();
            }

            textureAtlas = Resources.Load(atlasName) as Texture2D;
            AtlasMat = Resources.Load(atlasMatName, typeof(Material)) as Material;
        }


        //Initialize
        CreateStyles();
        colorPaletteLength = 0;
        workTex = new Texture2D(textureAtlas.width, textureAtlas.height);
        gameObject = Selection.activeObject as GameObject;
        gameObjectEditor = Editor.CreateEditor(gameObject);
        renderer = gameObject.GetComponent<Renderer>();
        if(gameObject.GetComponent<SkinnedMeshRenderer>() != null)
            mesh = gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        else
            mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

        //Count color palette
        for(int i = 0; i < textureAtlas.width * textureAtlas.height / 16; i ++)
            if(textureAtlas.GetPixel((2 + 4 * (i % textureAtlas.width)), (2 + 4 * (4 * i / textureAtlas.width))) == bgColor)
                break;
            else
                colorPaletteLength ++;

        //Building the palette that is writable
        workTex.SetPixels(0, 0, workTex.width, workTex.height, textureAtlas.GetPixels(0, 0, textureAtlas.width, textureAtlas.height));

        //If the object has a mesh, calculate the color picker
        if(mesh)
        {
            selectedColor = new int[mesh.subMeshCount];
            prevSelected = new int[mesh.subMeshCount];
            colors = new Texture2D[colorPaletteLength];

            for(int i = 0; i < colorPaletteLength; i ++)
            {
                colors[i] = new Texture2D(colorSize, colorSize);
                Color[] actualColor = new Color[colorSize * colorSize];
                for(int j = 0; j < colorSize * colorSize; j ++)
                    actualColor[j] = textureAtlas.GetPixel((2 + 4 * (i % textureAtlas.width)), (2 + 4 * (4 * i / textureAtlas.width)));
                
                colors[i].SetPixels(0, 0, colors[i].width, colors[i].height, actualColor);
                colors[i].Apply();
            }

            CheckForUvs();
        }

        //Debug
        if(debug)
            Debug.Log("The length of the color palette is " + colorPaletteLength);
    }

    void CheckForUvs()
    {
        for(int i = 0; i < mesh.subMeshCount; i ++)
        {
            Vector2 subMeshUV = new Vector2(0, 0);
            bool hasUVs = true;
            int[] tris = mesh.GetTriangles(i);
            if(mesh.uv.Length == 0)
                hasUVs = false;
            else
                subMeshUV = mesh.uv[tris[0]];

            for(int j = 0; j < tris.Length && hasUVs; j ++)
                if(mesh.uv[tris[j]] != subMeshUV)
                    hasUVs = false;
            
            if(hasUVs)
                selectedColor[i] = (int)(subMeshUV.x * textureAtlas.width - 2)/4;

            if(debug)
                if(!hasUVs)
                    Debug.Log("Didn't find any previous uv for submesh " + i);
                else
                    Debug.Log("Found UV for submesh " + i + " :    " + selectedColor[i]);
        }
    }

    //Create styles that will be used
    void CreateStyles()
    {
        colorStyle = new GUIStyle();
        colorStyle.fixedHeight = colorSize;
        colorStyle.fixedWidth = colorSize;

        editorStyle = GUIStyle.none;

        boxStyle = new GUIStyle();
        boxStyle.fixedHeight = 16 * colorSize;
        boxStyle.fixedWidth = 16 * colorSize;
        boxStyle.margin = new RectOffset((colorMenuWidth - 16 * colorSize)/2, 0, 0, 0);
        boxStyle.normal.background = Resources.Load("HashedTexture") as Texture2D;

        //Debug
        if(debug)
            Debug.Log("Created styles!");
    }

    bool CheckChanges()
    {
        bool shouldRemap = false;

        for(int i = 0; i < selectedColor.Length && !shouldRemap; i ++)
            if(selectedColor[i] != prevSelected[i])
                shouldRemap = true;

        return shouldRemap;
    }

    void RemapUV()
    {
        //TODO: Bad code, find a better way to do this
        Vector2[] uvs = new Vector2[mesh.vertexCount];

        //Setting uvs
        for(int i = 0; i < selectedColor.Length; i ++)
        {
            if(debug)
                Debug.Log("Selecionada a cor " + selectedColor[i] + " para a submesh " + i);

            int[] tris = mesh.GetTriangles(i);

            for(int j = 0; j < tris.Length; j ++)
                uvs[tris[j]] = new Vector2((texel/2 + texel * (selectedColor[i] % textureAtlas.width))/(float)textureAtlas.width, (texel/2 + texel * (texel * selectedColor[i] / textureAtlas.width))/(float)textureAtlas.height);
        }

        mesh.uv = uvs;
        Mesh tempMesh = (Mesh)UnityEngine.Object.Instantiate(mesh);
        AssetDatabase.CreateAsset(tempMesh, "Assets/ReMappedMeshes/" + gameObject.name + ".asset");
        gameObject.GetComponent<MeshFilter>().mesh = (Mesh)AssetDatabase.LoadAssetAtPath("Assets/ReMappedMeshes/" + gameObject.name + ".asset", typeof(Mesh));
    }

    void CopyColors()
    {     
        Material[] mats = gameObject.GetComponent<Renderer>().sharedMaterials;
        Color[] colorsToAdd = new Color[mats.Length];
        bool shouldCopy = true;

        for(int i = 0; i < colorsToAdd.Length && shouldCopy; i ++)
        {
            if(mats[i].mainTexture != null)
            {
                if(debug)
                    Debug.Log("Warning: Object has a material with a main texture. Couldn't complete the operation");
                shouldCopy = false;
            }

            colorsToAdd[i] = gameObject.GetComponent<Renderer>().sharedMaterials[i].color;
        }

        if(debug)
            Debug.Log("Successfully copied the colors to the texture atlas");

        if(shouldCopy)
            SetColor(colorsToAdd);
    }

    void AutoUVMap()
    {
        CopyColors();

        Material[] mats = new Material[mesh.subMeshCount];

        for(int i = 0; i < mesh.subMeshCount; i ++)
        {
            mats[i] = AtlasMat;
            selectedColor[i] = colorPaletteLength - mesh.subMeshCount + i;
        }

        gameObject.GetComponent<Renderer>().sharedMaterials = mats;

        if(debug)
            Debug.Log("Auto mapping the uvs of the mesh");

        RemapUV();
    }

    void ResetTextureAtlas()
    {
        workTex = new Texture2D(tSize, tSize);
        Color[] atlasColor = new Color[workTex.width * workTex.height];

        for(int i = 0; i < atlasColor.Length; i ++)
            atlasColor[i] = bgColor;

        workTex.SetPixels(0, 0, workTex.width, workTex.height, atlasColor);

        if(debug)
            Debug.Log("Reseted the Texture Atlas");

        SaveTex();
    }
}
