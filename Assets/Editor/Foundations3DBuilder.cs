using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Reproducible, original low-poly assets. Only writes the separate 3D edition.</summary>
public static class Foundations3DBuilder
{
    const string Root = "Assets/Foundations3D";
    static Mesh hex, cone, cylinder, rock;
    static Material soil, grass, moss, sand, bark, leaf, leafLight, stone, cream, leather, cloak, gold;
    [MenuItem("Foundations/Create 3D Edition")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding.");
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();
        soil = Mat("Cut earth", "574938"); grass = Mat("Sage meadow", "81976A");
        moss = Mat("Forest floor", "627E61"); sand = Mat("Ochre grass", "B6A373");
        bark = Mat("Dark walnut", "514337"); leaf = Mat("Pine deep", "315A4D"); leafLight = Mat("Pine sunlit", "557C5D");
        stone = Mat("Warm slate", "90988C"); cream = Mat("Linen", "E7D5AD"); leather = Mat("Russet leather", "845338");
        cloak = Mat("Hunter teal", "286366"); gold = Mat("Brass", "D4AD5F");
        hex = MeshAsset("Hexagonal plinth", Frustum(6, 1, 1));
        cone = MeshAsset("Faceted cone", Frustum(7, 1, 0));
        cylinder = MeshAsset("Octagonal shaft", Frustum(8, 1, 1));
        rock = MeshAsset("Hand-cut boulder", Boulder());
        var tree = MakeTree(); var treePrefab = SavePrefab(tree, "Fir tree");
        var boulder = new GameObject("Boulder"); Part(boulder.transform, "Stone", rock, stone, Vector3.zero, new Vector3(.38f,.48f,.32f));
        var rockPrefab = SavePrefab(boulder, "Boulder");
        var hunter = MakeHunter(); var hunterPrefab = SavePrefab(hunter, "Hunter miniature");
        var founder = MakeHunter(); founder.name = "Founder miniature";
        foreach (Transform child in new List<Transform>(founder.GetComponentsInChildren<Transform>()))
        {
            if (child == founder.transform) continue;
            if (child.name.Contains("Bow") || child.name.Contains("Arrow") || child.name.Contains("Fletching") || child.name.Contains("Quiver")) UnityEngine.Object.DestroyImmediate(child.gameObject);
            else if (child.name.Contains("Tunic") || child.name == "Hood" || child.name == "Cloak" || child.name == "Chest") child.GetComponent<Renderer>().sharedMaterial = cream;
        }
        Limb(founder.transform,"Walking staff",new Vector3(-.43f,.03f,.29f),new Vector3(-.43f,1.48f,.29f),.03f,bark);
        Part(founder.transform,"Travelling pack",rock,leather,new Vector3(0,.65f,-.30f),new Vector3(.23f,.50f,.19f));
        var founderPrefab = SavePrefab(founder,"Founder miniature");
        var settlement = new GameObject("Settlement");
        for (int i=0;i<3;i++)
        {
            float a=i*Mathf.PI*2/3; Vector3 p=new Vector3(Mathf.Cos(a)*.43f,0,Mathf.Sin(a)*.43f);
            Part(settlement.transform,"Roundhouse walls",cylinder,cream,p,new Vector3(.30f,.43f,.30f));
            Part(settlement.transform,"Thatched roof",cone,sand,p+Vector3.up*.43f,new Vector3(.38f,.40f,.38f));
        }
        Limb(settlement.transform,"Standard",Vector3.zero,Vector3.up*1.6f,.035f,bark);
        Part(settlement.transform,"Settlement banner",hex,cloak,new Vector3(0,1.17f,0),new Vector3(.20f,.30f,.055f));
        var cityPrefab = SavePrefab(settlement,"Settlement");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var gridObject = new GameObject("HexGrid");
        var grid = gridObject.AddComponent<HexGrid>(); grid.columns=512; grid.rows=256; grid.wrap=true; grid.chunkSize=16;
        grid.hexSize=1;grid.gap=0;grid.tileMaterial=grass;grid.waterMaterial=Mat("Still water","34666C");
        grid.borderMaterial=Mat("Hex ink","52634C",true);grid.borderWidth=.022f;
        grid.terrain3D=gridObject.AddComponent<Terrain3D>();grid.terrain3D.procedural=true;
        var generator=gridObject.AddComponent<MapGenerator>();generator.seed=42;
        var fog=gridObject.AddComponent<FogOfWar>();fog.fogMaterial=Mat("Fog veil","101C24",true);
        fog.fogMaterial.shader=Shader.Find("Foundations/FogVeil");
        fog.fogMaterial.SetFloat("_Surface",1);fog.fogMaterial.SetFloat("_Blend",0);
        fog.fogMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);fog.fogMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        fog.fogMaterial.SetFloat("_ZWrite",0);fog.fogMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");fog.fogMaterial.renderQueue=3000;
        fog.heightOffset=.48f;fog.unknownAlpha=1;fog.decayTurns=10;EditorUtility.SetDirty(fog.fogMaterial);
        var decorations=gridObject.AddComponent<WorldDecor3D>();decorations.treePrefab=treePrefab;decorations.rockPrefab=rockPrefab;
        gridObject.AddComponent<HexGridCulling>().grid=grid;
        MakeUnit(grid,"Founder",founderPrefab,true,2); MakeUnit(grid,"Hunter",hunterPrefab,false,3);
        var highlight=new GameObject("Selection").AddComponent<HexHighlight>();highlight.borderMaterial=Mat("Selection ivory","FFE9AD",true);highlight.radius=.94f;highlight.heightOffset=.05f;highlight.lineWidth=.045f;
        var path=new GameObject("Projected route").AddComponent<PathHighlight>();path.lineMaterial=Mat("Route gold","FFD16D",true);path.heightOffset=.12f;path.lineWidth=.065f;
        var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";var cam=cameraObject.AddComponent<Camera>();
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.06f,.11f,.14f);cam.fieldOfView=43;cam.nearClipPlane=.1f;cam.farClipPlane=220;
        cameraObject.AddComponent<AudioListener>();cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraObject.transform.rotation=Quaternion.Euler(49,0,0);cameraObject.transform.position=new Vector3(8,15,-7);
        var pan=cameraObject.AddComponent<CameraPan>();pan.grid=grid;pan.cam=cam;pan.panSpeed=7;pan.zoomSpeed=.018f;pan.minZoomHeight=5;pan.maxZoomHeight=45;
        cameraObject.AddComponent<WorldOrbit3D>();
        var sun=new GameObject("Late afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=2;sun.color=new Color(1,.88f,.69f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-38,0);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.56f,.66f,.69f);RenderSettings.ambientEquatorColor=new Color(.36f,.43f,.38f);RenderSettings.ambientGroundColor=new Color(.20f,.24f,.22f);
        var game=new GameObject("GameController").AddComponent<GameController>();game.grid=grid;game.cam=cam;game.cameraPan=pan;game.fogOfWar=fog;game.selectionHighlight=highlight;game.pathHighlight=path;game.use3DHud=true;game.cityPrefab3D=cityPrefab;
        game.cityTileHeight=.07f;game.gameObject.AddComponent<ExpeditionHUD>().game=game;
        var minimap=game.gameObject.AddComponent<Minimap>();minimap.grid=grid;minimap.fogOfWar=fog;minimap.bottomOffset=155;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Foundations3D.unity");AssetDatabase.SaveAssets();
        Debug.Log("Reconciled 3D world: 512x256, Founder + Hunter, city founding, growth, minimap, and temporal fog.");
    }
    static void MakeUnit(HexGrid grid,string name,GameObject prefab,bool canFound,int sight)
    {
        var go=new GameObject(name);var collider=go.AddComponent<CapsuleCollider>();collider.center=Vector3.up*.8f;collider.height=1.7f;collider.radius=.36f;
        var unit=go.AddComponent<Unit>();unit.grid=grid;unit.unitName=name;unit.canFoundCity=canFound;unit.sightRadius=sight;unit.heightOffset=.015f;
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,go.transform);model.transform.localRotation=Quaternion.Euler(0,180,0);
        model.AddComponent<HunterVisual3D>().unit=unit;
    }
    static Material Mat(string name,string color,bool unlit=false)
    {
        string path=Root+"/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        ColorUtility.TryParseHtmlString("#"+color,out Color c);mat.SetColor("_BaseColor",c);mat.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(mat);return mat;
    }
    static Mesh MeshAsset(string name,Mesh mesh)
    {
        string path=Root+"/Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        mesh.name=name;
        if(existing!=null){UnityEngine.Object.DestroyImmediate(mesh);return existing;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static GameObject Part(Transform parent,string name,Mesh mesh,Material mat,Vector3 position,Vector3 scale)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go;
    }
    static Mesh FlatHex()
    {
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<6;i++){float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;Tri(v,t,Vector3.zero,new Vector3(Mathf.Cos(b),0,Mathf.Sin(b)),new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
        return Finish(v,t);
    }
    static Mesh Frustum(int count,float lower,float upper)
    {
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<count;i++)
        {
            float a=2*Mathf.PI*i/count,b=2*Mathf.PI*(i+1)/count;
            Vector3 p=new Vector3(Mathf.Cos(a)*lower,0,Mathf.Sin(a)*lower),q=new Vector3(Mathf.Cos(b)*lower,0,Mathf.Sin(b)*lower);
            Vector3 u=new Vector3(Mathf.Cos(a)*upper,1,Mathf.Sin(a)*upper),w=new Vector3(Mathf.Cos(b)*upper,1,Mathf.Sin(b)*upper);
            Tri(v,t,p,u,w);Tri(v,t,p,w,q);Tri(v,t,Vector3.up,w,u);Tri(v,t,Vector3.zero,p,q);
        }
        return Finish(v,t);
    }
    static Mesh Boulder()
    {
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<7;i++)
        {
            float a=i*2*Mathf.PI/7,b=(i+1)*2*Mathf.PI/7;
            Vector3 p=new Vector3(Mathf.Cos(a),.22f,Mathf.Sin(a)),q=new Vector3(Mathf.Cos(b),.22f,Mathf.Sin(b));
            Tri(v,t,p,new Vector3(.13f,1,-.1f),q);Tri(v,t,p,q,new Vector3(0,0,0));
        }
        return Finish(v,t);
    }
    static void Tri(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c){int n=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(n);t.Add(n+1);t.Add(n+2);}
    static Mesh Finish(List<Vector3> v,List<int> t){var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
    static GameObject SavePrefab(GameObject obj,string name){var prefab=PrefabUtility.SaveAsPrefabAsset(obj,Root+"/Prefabs/"+name+".prefab");UnityEngine.Object.DestroyImmediate(obj);return prefab;}
    static GameObject MakeTree()
    {
        var tree=new GameObject("Fir tree");Part(tree.transform,"Trunk",cylinder,bark,Vector3.zero,new Vector3(.075f,.85f,.075f));
        Part(tree.transform,"Lower crown",cone,leaf,new Vector3(0,.4f,0),new Vector3(.44f,.8f,.44f));
        Part(tree.transform,"Middle crown",cone,leafLight,new Vector3(0,.78f,0),new Vector3(.34f,.75f,.34f));
        Part(tree.transform,"Tip",cone,leaf,new Vector3(0,1.13f,0),new Vector3(.22f,.65f,.22f));return tree;
    }
    static void Limb(Transform parent,string name,Vector3 a,Vector3 b,float width,Material mat)
    {
        var go=Part(parent,name,cylinder,mat,a,new Vector3(width,Vector3.Distance(a,b),width));go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    static GameObject MakeHunter()
    {
        var h=new GameObject("Hunter miniature");var p=h.transform;
        Part(p,"Boot left",rock,leather,new Vector3(-.13f,.02f,.06f),new Vector3(.115f,.16f,.21f));
        Part(p,"Boot right",rock,leather,new Vector3(.13f,.02f,.06f),new Vector3(.115f,.16f,.21f));
        Limb(p,"Left leg",new Vector3(-.13f,.12f,0),new Vector3(-.12f,.62f,0),.085f,leather);
        Limb(p,"Right leg",new Vector3(.13f,.12f,0),new Vector3(.12f,.62f,0),.085f,leather);
        Part(p,"Tunic skirt",cone,cloak,new Vector3(0,.47f,0),new Vector3(.31f,.69f,.23f));
        Part(p,"Chest",cylinder,cloak,new Vector3(0,.72f,0),new Vector3(.22f,.38f,.16f));
        Part(p,"Belt",cylinder,leather,new Vector3(0,.73f,0),new Vector3(.239f,.065f,.175f));
        Part(p,"Buckle",rock,gold,new Vector3(0,.74f,.17f),new Vector3(.055f,.06f,.035f));
        Part(p,"Head",rock,cream,new Vector3(0,1.12f,.025f),new Vector3(.17f,.32f,.17f));
        Part(p,"Hood",cone,cloak,new Vector3(0,1.27f,-.03f),new Vector3(.21f,.31f,.21f));
        Part(p,"Cloak",cone,cloak,new Vector3(0,.53f,-.19f),new Vector3(.32f,.67f,.16f));
        Limb(p,"Left sleeve",new Vector3(-.22f,1.02f,0),new Vector3(-.36f,.84f,.13f),.10f,cloak);
        Limb(p,"Left forearm",new Vector3(-.36f,.84f,.13f),new Vector3(-.43f,.93f,.29f),.065f,cream);
        Limb(p,"Right sleeve",new Vector3(.22f,1.02f,0),new Vector3(.33f,.80f,.10f),.10f,cloak);
        Limb(p,"Right forearm",new Vector3(.33f,.80f,.10f),new Vector3(.22f,.78f,.28f),.065f,cream);
        // Segmented curved bow and a taut string, all actual mesh geometry.
        Vector3 previous=new Vector3(-.43f,.39f,.27f);
        for(int i=1;i<=9;i++){float a=i/9f;Vector3 next=new Vector3(-.43f,.39f+a*1.03f,.27f+Mathf.Sin(a*Mathf.PI)*.22f);Limb(p,"Bow stave",previous,next,.026f,leather);previous=next;}
        Limb(p,"Bow string",new Vector3(-.43f,.39f,.27f),new Vector3(-.43f,1.42f,.27f),.006f,cream);
        Limb(p,"Quiver",new Vector3(.21f,.71f,-.22f),new Vector3(.32f,1.20f,-.26f),.09f,leather);
        for(int i=0;i<3;i++){var a=new Vector3(.28f+i*.035f,1.12f,-.26f);Limb(p,"Arrow",a,a+new Vector3(.04f,.30f,0),.009f,bark);Part(p,"Fletching",cone,cream,a+new Vector3(.04f,.27f,0),new Vector3(.035f,.09f,.025f));}
        return h;
    }
}
