using System.Collections.Generic;
using UnityEngine;

/// <summary>Original 3D props only for currently visible tiles; no per-tile objects across the full world.</summary>
public class WorldDecor3D : MonoBehaviour
{
    public GameObject treePrefab, rockPrefab;
    HexGrid grid; FogOfWar fog; int revision=-1,cityCount=-1;
    readonly Dictionary<Vector2Int,GameObject> props=new Dictionary<Vector2Int,GameObject>();
    void Start(){grid=GetComponent<HexGrid>();fog=GetComponent<FogOfWar>();}
    void LateUpdate()
    {
        var cities=FindObjectsByType<City>(FindObjectsSortMode.None);
        if (fog.StateRevision!=revision || cityCount!=cities.Length)
        {
            cityCount=cities.Length;
            revision=fog.StateRevision;var wanted=new HashSet<Vector2Int>();
            foreach(var u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            {grid.TryWorldToHex(u.transform.position,out int c,out int r);AddDisk(wanted,c,r,u.sightRadius);}
            foreach(var c in cities)AddDisk(wanted,c.col,c.row,City.TerritoryRadius);
            foreach(var city in cities)wanted.Remove(city.Coord);
            var remove=new List<Vector2Int>();foreach(var kv in props)if(!wanted.Contains(kv.Key)){Destroy(kv.Value);remove.Add(kv.Key);}
            foreach(var key in remove)props.Remove(key);
            foreach(var tile in wanted)
            {
                if(props.ContainsKey(tile)||!grid.IsLand(tile.x,tile.y))continue;
                int hash=System.Math.Abs(unchecked(tile.x*73856093^tile.y*19349663));
                if(hash%3==0)continue;
                var go=Instantiate(hash%5==0?rockPrefab:treePrefab,transform);go.name=$"Terrain prop {tile}";
                go.transform.localScale=Vector3.one*(.65f+(hash%23)*.01f);props[tile]=go;
            }
        }
        var cam=Camera.main;
        foreach(var kv in props)
        {
            var p=grid.HexCenter(kv.Key.x,kv.Key.y)+new Vector3(.45f,0,.32f);
            kv.Value.transform.position=grid.WrapToCamera(p,cam.transform.position.x);
        }
    }
    void AddDisk(HashSet<Vector2Int> into,int c,int r,int radius)
    {
        for(int x=c-radius;x<=c+radius;x++)for(int y=r-radius;y<=r+radius;y++)
        {int col=grid.WrapColumn(x);if(grid.InBounds(col,y)&&grid.Distance(c,r,col,y)<=radius&&fog.GetVisibility(col,y)==TileVisibility.Visible)into.Add(new Vector2Int(col,y));}
    }
}
