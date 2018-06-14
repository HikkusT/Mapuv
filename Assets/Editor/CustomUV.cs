using UnityEngine;
using UnityEditor;
using System.IO;

public class CustomUV : EditorWindow {
    static bool refresh = false;
    bool debug = true;

    #region Atlas settings
    Material AtlasMat;
    int tSize = 64;
    Color bgColor = Color.magenta;
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
    #endregion

	GameObject gameObject;
    Mesh mesh;
    Texture2D textureAtlas = null;
    Texture2D workTex = null;
    Texture2D[] colors;
    Editor gameObjectEditor;
    Color colorToAdd;
    int colorPaletteLength = 0;
    int[] selectedColor;
    int[] prevSelected;

    [MenuItem("Assets/UV Mapper")]
    static void ShowWindow()
    {
        GetWindow<CustomUV>("Simple UV Mapper");
        refresh = true;
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



        GUILayout.Label(gameObject.name);


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
        colorToAdd = EditorGUILayout.ColorField(new GUIContent(), colorToAdd, true, true, false, GUILayout.Width(colorMenuWidth/2));

        if(GUILayout.Button("Add Color", GUILayout.Width(colorMenuWidth/2 - 15)))
        {
            SetColor(4, colorToAdd);
        }

        GUILayout.EndHorizontal();

        if(GUILayout.Button("Auto"))
        {
            AutoUVMap();
        }

        if(GUILayout.Button("Force Refresh"))
        {
            Refresh();
        }

        if(GUILayout.Button("Copy colors"))
        {
            CopyColors();
        }

        if(GUILayout.Button("Reset"))
        {
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
        File.WriteAllBytes(Application.dataPath + "/Resources/TextureAtlas.png", bytes);
        AssetDatabase.Refresh();

        Refresh();
    }

    //Set a color in the next avaiable spot
    void SetColor(int size, Color color)
    {
        if(debug)
            Debug.Log("Adding color " + color);

        for(int i = 0; i < size; i ++)
            for(int j = 0; j < size; j ++)
                workTex.SetPixel((j + size * colorPaletteLength) % textureAtlas.width, i + size * (int)Mathf.Floor((size * colorPaletteLength / textureAtlas.width)), color);

        colorPaletteLength ++;

        SaveTex();
    }

    void SetColor(int size, Color[] color)
    {
        for(int z = 0; z < color.Length; z ++)
        {
            if(debug)
                Debug.Log("Adding color " + color[z]);

            for(int i = 0; i < size; i ++)
                for(int j = 0; j < size; j ++)
                    workTex.SetPixel((j + size * colorPaletteLength) % textureAtlas.width, i + size * (int)Mathf.Floor((size * colorPaletteLength / textureAtlas.width)), color[z]);

            colorPaletteLength ++;
        }

        SaveTex();
    }

    //Force Refresh
    void Refresh()
    {
        //Initialize
        CreateStyles();
        colorPaletteLength = 0;
        AtlasMat = Resources.Load("TextureAtlasMat", typeof(Material)) as Material;
        textureAtlas = Resources.Load("TextureAtlas") as Texture2D;
        workTex = new Texture2D(textureAtlas.width, textureAtlas.height);
        gameObject = Selection.activeObject as GameObject;
        gameObjectEditor = Editor.CreateEditor(gameObject);
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
            selectedColor = new int[gameObject.GetComponent<MeshFilter>().sharedMesh.subMeshCount];
            prevSelected = new int[gameObject.GetComponent<MeshFilter>().sharedMesh.subMeshCount];
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
            Vector2 subMeshUV;
            bool hasUVs = true;
            int[] tris = mesh.GetTriangles(i);
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
                uvs[tris[j]] = new Vector2((2 + 4 * (selectedColor[i] % textureAtlas.width))/(float)textureAtlas.width, (2 + 4 * (4 * selectedColor[i] / textureAtlas.width))/(float)textureAtlas.height);
        }

        mesh.uv = uvs;
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
            SetColor(4, colorsToAdd);
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

        Refresh();
    }
}
