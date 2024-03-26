// MIT License
//
// Copyright (c) 2023 Roland Helmerichs
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Generic;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;
using FileAccess = Godot.FileAccess;
using System;
using System.Globalization;

namespace YATI;

public class TilesetCreator
{
    private const string WarningColor = "Yellow";
    private const string CustomDataInternal = "__internal__";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private TileSet _tileset;
    private TileSetAtlasSource _currentAtlasSource;
    private int _currentMaxX;
    private int _currentMaxY;
    private string _basePathMap;
    private string _basePathTileset;
    private int _terrainSetsCounter = -1;
    private int _terrainCounter;
    private int _tileCount;
    private int _columns;
    private Vector2I _tileSize;
    private int _physicsLayerCounter = -1;
    private int _navigationLayerCounter = -1;
    private int _occlusionLayerCounter = -1;
    private bool _append;
    private Array<Dictionary> _atlasSources;
    private int _errorCount;
    private int _warningCount;
    private Vector2I _mapTileSize;
    private Vector2I _gridSize;
    private Vector2I _tileOffset;
    private string _objectAlignment;
    private Dictionary _objectGroups;
    private int _objectGroupsCounter;
    private string _tilesetOrientation;
    private bool _mapWangsetToTerrain;
    private CustomTypes _ct;
    private int _currentFirstGid = -1;

    private enum LayerType
    {
        Physics,
        Navigation,
        Occlusion
    }

    public int GetErrorCount()
    {
        return _errorCount;
    }

    public int GetWarningCount()
    {
        return _warningCount;
    }

    public void SetBasePath(string sourceFile)
    {
        _basePathMap = sourceFile.GetBaseDir();
        _basePathTileset = _basePathMap;
    }

    public void SetMapParameters(Vector2I mapTileSize)
    {
        _mapTileSize = mapTileSize;
    }

    public void SetCustomTypes(CustomTypes ct)
    {
        _ct = ct;
    }

    public void MapWangsetToTerrain()
    {
        _mapWangsetToTerrain = true;
    }
    
    public TileSet CreateFromDictionaryArray(Array<Dictionary> tileSets)
    {
        foreach (var tileSet in tileSets)
        {
            var tileSetDict = tileSet;
            if (tileSet.TryGetValue("source", out var srcVal))
            {
                var checkedFile = (string)srcVal;
                
                // Catch the AutoMap Rules tileset (is Tiled internal)
                if (checkedFile.StartsWith(":/automap"))
                    continue; // This is no error just skip it
                
                if (!FileAccess.FileExists(checkedFile))
                    checkedFile = _basePathMap.PathJoin(checkedFile);
                _basePathTileset = checkedFile.GetBaseDir();

                tileSetDict = DictionaryBuilder.GetDictionary(checkedFile);
                if (tileSetDict != null && tileSet.TryGetValue("firstgid", out var firstGid))
                    tileSetDict["firstgid"] = firstGid;
            }

            // Possible error condition
            if (tileSetDict == null)
            {
                _errorCount++;
                continue;
            }

            CreateOrAppend(tileSetDict);
            _append = true;
        }

        return _tileset;
    }

    public TileSet CreateFromFile(string sourceFile)
    {
        var tileSet = DictionaryBuilder.GetDictionary(sourceFile);
        CreateOrAppend(tileSet);
        return _tileset;
    }

    public Array<Dictionary> GetRegisteredAtlasSources()
    {
        return _atlasSources;
    }

    public Dictionary GetRegisteredObjectGroups()
    {
        return _objectGroups;
    }

    private void CreateOrAppend(Dictionary tileSet)
    {
        // Catch the AutoMap Rules tileset (is Tiled internal)
        if (tileSet.ContainsKey("name") && ((string)tileSet["name"] == "AutoMap Rules"))
            return; // This is no error just skip it

        if (!_append)
        {
            _tileset = new TileSet();
            _tileset.AddCustomDataLayer();
            _tileset.SetCustomDataLayerName(0, CustomDataInternal);
            _tileset.SetCustomDataLayerType(0, Variant.Type.Int);
        }

        _tileSize = new Vector2I((int)tileSet["tilewidth"], (int)tileSet["tileheight"]);
        if (!_append)
            _tileset.TileSize = _mapTileSize;
        _tileCount = tileSet.TryGetValue("tilecount", out var tileCount) ? (int)tileCount : 0;
        _columns = tileSet.TryGetValue("columns", out var columns) ? (int)columns : 0;
        _tilesetOrientation = "orthogonal";
        _gridSize = _tileSize;
        if (tileSet.TryGetValue("tileoffset", out var toVal))
        {
            var to = (Dictionary)toVal;
            _tileOffset = new Vector2I((int)to["x"], (int)to["y"]);
        }
        else
            _tileOffset = Vector2I.Zero;

        _currentFirstGid = tileSet.TryGetValue("firstgid", out var firstgid) ? (int)firstgid : -1;

        if (tileSet.TryGetValue("grid", out var gridVal))
        {
            var grid = (Dictionary)gridVal;
            if (grid.TryGetValue("orientation", out var orient))
                _tilesetOrientation = (string)orient;
            _gridSize.X = (int)grid.GetValueOrDefault("width", _tileSize.X);
            _gridSize.Y = (int)grid.GetValueOrDefault("height", _tileSize.Y);
        }

        if (tileSet.TryGetValue("objectalignment", out var objAlignment))
            _objectAlignment = (string)objAlignment;
        else
            _objectAlignment = "unspecified";
        
        if (_append)
            _terrainCounter = 0;

        if (tileSet.TryGetValue("image", out var imagePath))
        {
            _currentAtlasSource = new TileSetAtlasSource();
            var addedSourceId = _tileset.AddSource(_currentAtlasSource);
            _currentAtlasSource.TextureRegionSize = _tileSize;
            if (tileSet.ContainsKey("margin"))
                _currentAtlasSource.Margins = new Vector2I((int)tileSet["margin"], (int)tileSet["margin"]);
            if (tileSet.ContainsKey("spacing"))
                _currentAtlasSource.Separation = new Vector2I((int)tileSet["spacing"], (int)tileSet["spacing"]);
            
            var texture = LoadImage((string)imagePath);
            if (texture == null)
                // Can't continue without texture
                return;

            _currentAtlasSource.Texture = texture;
 
            if ((_tileCount == 0) || (_columns == 0))
            {
                var imagewidth = tileSet.TryGetValue("imagewidth", out var imgWidth) ? (int)imgWidth : 0;
                var imageheight = tileSet.TryGetValue("imageheight", out var imgHeight) ? (int)imgHeight : 0;
                if (imagewidth == 0)
                {
                    var img = _currentAtlasSource.Texture;
                    imagewidth = img.GetWidth();
                    imageheight = img.GetHeight();
                }
                _columns = imagewidth / _tileSize.X;
                _tileCount = _columns * imageheight / _tileSize.X;
            }
            RegisterAtlasSource(addedSourceId, _tileCount, -1, _tileOffset);
            var atlasGridSize = _currentAtlasSource.GetAtlasGridSize();
            _currentMaxX = atlasGridSize.X - 1;
            _currentMaxY = atlasGridSize.Y - 1;
        }
        
        if (tileSet.TryGetValue("tiles", out var tiles))
            HandleTiles((Array<Dictionary>)tiles);
        if (tileSet.TryGetValue("wangsets", out var wangsets))
        {
            if (_mapWangsetToTerrain)
                HandleWangsetsOldMapping((Array<Dictionary>)wangsets);
            else
                HandleWangsets((Array<Dictionary>)wangsets);
        }
        
        _ct?.MergeCustomProperties(tileSet, "tileset");
        
        if (tileSet.TryGetValue("properties", out var props))
            HandleTilesetProperties((Array<Dictionary>)props);
    }

    private Texture2D LoadImage(string path)
    {
        var origPath = path;
        Texture2D ret = null;
        // ToDo: Not sure if this first check makes any sense since an image can't be imported properly if not in project tree
        if (!FileAccess.FileExists(path))
            path = _basePathMap.GetBaseDir().PathJoin(origPath);
        if (!FileAccess.FileExists(path))
            path = _basePathTileset.PathJoin(origPath);
        if (FileAccess.FileExists(path))
        {
            var exists = ResourceLoader.Exists(path, "Image");
            if (exists)
                ret = (Texture2D)ResourceLoader.Load(path, "Image");
            else
            {
                var image = Image.LoadFromFile(path);
                ret = ImageTexture.CreateFromImage(image);
            }
        }
        else
        {
            GD.PrintErr($"ERROR: Image file '{origPath}' not found.");
            _errorCount++;
        }

        return ret;
    }

    private void RegisterAtlasSource(int sourceId, int numTiles, int assignedTileId, Vector2I tileOffset)
    {
        _atlasSources ??= new Array<Dictionary>();
        var atlasSourceItem = new Dictionary();
        atlasSourceItem.Add("sourceId", sourceId);
        atlasSourceItem.Add("numTiles", numTiles);
        atlasSourceItem.Add("assignedId", assignedTileId);
        atlasSourceItem.Add("tileOffset", tileOffset);
        atlasSourceItem.Add("tilesetOrientation", _tilesetOrientation);
        atlasSourceItem.Add("objectAlignment", _objectAlignment);
        atlasSourceItem.Add("firstGid", _currentFirstGid);
        _atlasSources.Add(atlasSourceItem);
    }

    private void RegisterObjectGroup(int tileId, Dictionary objectGroup)
    {
        _objectGroups ??= new Dictionary();
        _objectGroups.Add(tileId, objectGroup);
    }

    private TileData CreateTileIfNotExistingAndGetTileData(int tileId)
    {
        if (tileId < _tileCount)
        {
            var row = tileId / _columns;
            var col = tileId % _columns;
            var tileCoords = new Vector2I(col, row);
            if (col > _currentMaxX || row > _currentMaxY)
            {
                GD.PrintRich($"[color={WarningColor}] -- Tile {tileId} at {col},{row} outside texture range. -> Skipped[/color]");
                _warningCount++;
                return null;
            }
            var tileAtCoords = _currentAtlasSource.GetTileAtCoords(tileCoords);
            if (tileAtCoords == new Vector2I(-1, -1))
                _currentAtlasSource.CreateTile(tileCoords);
            else if (tileAtCoords != tileCoords)
            {
                GD.PrintRich($"[color={WarningColor}]WARNING: tileAtCoords not equal tileCoords![/color]");
                GD.PrintRich($"[color={WarningColor}]         tileCoords:   {col},{row}[/color]");
                GD.PrintRich($"[color={WarningColor}]         tileAtCoords: {tileAtCoords.X},{tileAtCoords.X}[/color]");
                GD.PrintRich($"[color={WarningColor}]-> Tile skipped[/color]");
                _warningCount++;
                return null;
            }
            return _currentAtlasSource.GetTileData(tileCoords, 0);
        }
        GD.PrintRich($"[color={WarningColor}] -- Tile {tileId} outside tile count range (0-{_tileCount-1}). -> Skipped[/color]");
        _warningCount++;
        return null;
    }

    private void HandleTiles(Array<Dictionary> tiles)
    {
        foreach (var tile in tiles)
        {
            var tileId = (int)tile["id"];

            TileData currentTile;
            if (tile.ContainsKey("image"))
            {
                // Tile with it's own image -> separate atlas source
                _currentAtlasSource = new TileSetAtlasSource();
                var addedSourceId = _tileset.AddSource(_currentAtlasSource);
                RegisterAtlasSource(addedSourceId, 1, tileId, Vector2I.Zero);
                
                var texturePath = (string)tile["image"];
                _currentAtlasSource.Texture = LoadImage(texturePath);

                // ToDo: The following code is a (C# version only) workaround against possible racing conditions
                var i = 0;
                while (_currentAtlasSource.Texture == null)
                {
                    _currentAtlasSource.Texture = LoadImage((string)tile["image"]);
                    i++;
                    if (i <= 10) continue;
                    GD.PrintErr("Failed over 10 times to load");
                    _errorCount++;
                    break;
                }
                _currentAtlasSource.ResourceName = texturePath.GetFile().GetBaseName();
                var textureWidth = _currentAtlasSource.Texture.GetWidth();
                if (tile.TryGetValue("width", out var tileWidth))
                    textureWidth = (int)tileWidth;
                var textureHeight = _currentAtlasSource.Texture.GetHeight();
                if (tile.TryGetValue("height", out var tileHeight))
                    textureHeight = (int)tileHeight;
                _currentAtlasSource.TextureRegionSize = new Vector2I(textureWidth, textureHeight);
                var tileOffsetX = 0;
                if (tile.TryGetValue("x", out var offsetX))
                    tileOffsetX = (int)offsetX;
                var tileOffsetY = 0;
                if (tile.TryGetValue("y", out var offsetY))
                    tileOffsetY = (int)offsetY;
                _currentAtlasSource.Margins = new Vector2I(tileOffsetX, tileOffsetY);
                
                _currentAtlasSource.CreateTile(Vector2I.Zero);
                currentTile = _currentAtlasSource.GetTileData(Vector2I.Zero, 0);
                currentTile.Probability = (float)tile.GetValueOrDefault("probability", 1.0f);
            }
            else
            {
                currentTile = CreateTileIfNotExistingAndGetTileData(tileId);
                if (currentTile == null)
                    //Error occurred
                    continue;
            }

            if (_tileSize.X != _mapTileSize.X || _tileSize.Y != _mapTileSize.Y)
            {
                var diffX = _tileSize.X - _mapTileSize.X;
                if (diffX % 2 != 0)
                    diffX -= 1;
                var diffY = _tileSize.Y - _mapTileSize.Y;
                if (diffY % 2 != 0)
                    diffY += 1;
                currentTile.TextureOrigin = new Vector2I(-diffX/2, diffY/2) - _tileOffset;
            }
            
            if (tile.TryGetValue("probability", out var probVal))
                currentTile.Probability = (float)probVal;
            if (tile.TryGetValue("animation", out var animVal))
                HandleAnimation((Array<Dictionary>)animVal, tileId);
            if (tile.TryGetValue("objectgroup", out var objgrp))
                HandleObjectgroup((Dictionary)objgrp, currentTile, tileId);

            _ct?.MergeCustomProperties(tile, "tile");
            if (tile.TryGetValue("properties", out var props))
                HandleTileProperties((Array<Dictionary>)props, currentTile);
        }
    }

    private void HandleAnimation(Array<Dictionary> frames, int tileId)
    {
        var frameCount = 0;
        var separationX = 0;
        var separationY = 0;
        var separationVect = new Vector2I(separationX, separationY);
        var animColumns = 0;
        var tileCoords = new Vector2I(tileId % _columns, tileId / _columns);
        var maxDiffX = _columns - tileCoords.X;
        var maxDiffY = _tileCount / _columns - tileCoords.Y;
        var diffX = 0;
        var diffY = 0;
        foreach (var frame in frames)
        {
            frameCount++;
            var frameTileId = (int)frame["tileid"];
            if (frameCount == 2)
            {
                diffX = (frameTileId - tileId) % _columns;
                diffY = (frameTileId - tileId) / _columns;
                if (diffX == 0 && diffY > 0 && diffY < maxDiffY)
                {
                    separationY = diffY - 1;
                    animColumns = 1;
                }
                else if (diffY == 0 && diffX > 0 && diffX < maxDiffX)
                {
                    separationX = diffX - 1;
                    animColumns = 0;
                }
                else
                {
                    GD.PrintRich(
                        $"[color={WarningColor}] -- Animated tile {tileId}: Succession of tiles not supported in Godot 4. -> Skipped[/color]");
                    _warningCount++;
                    return;
                }

                separationVect = new Vector2I(separationX, separationY);
            }
            if (frameCount > 1 && frameCount < frames.Count) 
            {
                var nextFrameTileId = (int)frames[frameCount]["tileid"];
                var compareDiffX = (nextFrameTileId - frameTileId) % _columns;
                var compareDiffY = (nextFrameTileId - frameTileId) / _columns;
                if ((compareDiffX != diffX) || (compareDiffY != diffY))
                {
                    GD.PrintRich($"[color={WarningColor}] -- Animated tile {tileId}: Succession of tiles not supported in Godot 4. -> Skipped[/color]");
                    _warningCount++;
                    return;
                }
            }
 
            if (_currentAtlasSource.HasRoomForTile(tileCoords, Vector2I.One, animColumns, separationVect, frameCount, tileCoords))
            {
                _currentAtlasSource.SetTileAnimationSeparation(tileCoords, separationVect);
                _currentAtlasSource.SetTileAnimationColumns(tileCoords, animColumns);
                _currentAtlasSource.SetTileAnimationFramesCount(tileCoords,frameCount);
                var durationInSecs = 1.0f;
                if (frame.TryGetValue("duration", out var duration))
                    durationInSecs = (float)duration / 1000.0f;
                _currentAtlasSource.SetTileAnimationFrameDuration(tileCoords,frameCount-1, durationInSecs);
            }
            else
            {
                GD.PrintRich($"[color={WarningColor}] -- TileId {tileId}: Not enough room for all animation frames, could only set {frameCount} frames.[/color]");
                _warningCount++;
                break;
            }
        }        
    }

    private void HandleObjectgroup(Dictionary objectGroup, TileData currentTile, int tileId)
    {
        // v1.2
        _objectGroupsCounter++;
        RegisterObjectGroup(_objectGroupsCounter, objectGroup);
        currentTile.SetCustomData(CustomDataInternal, _objectGroupsCounter);
        
        var objects = (Array<Dictionary>)objectGroup["objects"];
        var polygonIndices = new Dictionary();
        foreach (var obj in objects)
        {
            if (obj.ContainsKey("point") && (bool)obj["point"])
            {
                //GD.PrintRich($"[color={WarningColor}] -- 'Point' has currently no corresponding tileset element in Godot 4. -> Skipped[/color]");
                //_warningCount++;
                break;
            }
            if (obj.ContainsKey("ellipse") && (bool)obj["ellipse"])
            {
                //GD.PrintRich($"[color={WarningColor}] -- 'Ellipse' has currently no corresponding tileset element in Godot 4. -> Skipped[/color]");
                //_warningCount++;
                break;
            }
            
            _ct?.MergeCustomProperties(obj, "object");
            
            var objectBaseCoords = new Vector2((float)obj["x"], (float)obj["y"]);
            objectBaseCoords = TransposeCoords(objectBaseCoords.X, objectBaseCoords.Y);
            objectBaseCoords -= currentTile.TextureOrigin;
            if (_tilesetOrientation == "isometric")
            {
                objectBaseCoords.Y -= _gridSize.Y / 2.0f;
                if (_gridSize.Y != _tileSize.Y)
                    objectBaseCoords.Y += (_tileSize.Y - _gridSize.Y) / 2.0f;
            }
            else
                objectBaseCoords -= (Vector2)_tileSize / 2.0f;

            var rot = (float)obj.GetValueOrDefault("rotation", 0.0f);
            var sinA = (float)Math.Sin(rot * Math.PI / 180.0f);
            var cosA = (float)Math.Cos(rot * Math.PI / 180.0f);

            Vector2[] polygon;
            if (obj.TryGetValue("polygon", out var pts) || obj.TryGetValue("polyline", out pts))
            {
                var polygonPoints = (Array<Dictionary>)pts;
                if (polygonPoints.Count < 3)
                {
                    GD.PrintRich($"[color={WarningColor}] -- Skipped invalid polygon on tile {tileId} (less than 3 points)[/color]");
                    _warningCount++;
                    break;
                }
                polygon = new Vector2[polygonPoints.Count];
                var i = 0;
                foreach (var pt in polygonPoints)
                {
                    var pCoord = TransposeCoords((float)pt["x"], (float)pt["y"]);
                    var pCoordRot = new Vector2(pCoord.X * cosA - pCoord.Y * sinA, pCoord.X * sinA + pCoord.Y * cosA);
                    polygon[i] = objectBaseCoords + pCoordRot;
                    i++;
                }
            }
            else
            {
                // Should be a simple rectangle
                polygon = new Vector2[4];
                polygon[0] = Vector2.Zero;
                polygon[1].X = polygon[0].X;
                polygon[1].Y = polygon[0].Y + (float)obj["height"];
                polygon[2].X = polygon[0].X + (float)obj["width"];
                polygon[2].Y = polygon[1].Y;
                polygon[3].X = polygon[2].X;
                polygon[3].Y = polygon[0].Y;
                var i = 0;
                foreach (var pt in polygon)
                {
                    var ptTrans = TransposeCoords(pt.X, pt.Y);
                    var ptRot = new Vector2(ptTrans.X * cosA - ptTrans.Y * sinA, ptTrans.X * sinA + ptTrans.Y * cosA);
                    polygon[i] = objectBaseCoords + ptRot;
                    i++;
                }
            }

            var nav = GetLayerNumberForSpecialProperty(obj, "navigation_layer");
            if (nav >= 0)
            {
                var navP = new NavigationPolygon();
                navP.AddOutline(polygon);
                //navP.MakePolygonsFromOutlines();
                // Replaced in 4.2 deprecated function MakePolygonsFromOutlines
                navP.Vertices = polygon;
                var pg = new int[navP.Vertices.Length];
                for (var idx = 0; idx < navP.Vertices.Length; idx++)
                    pg[idx] = idx;
                navP.AddPolygon(pg);
                EnsureLayerExisting(LayerType.Navigation, nav);
                currentTile.SetNavigationPolygon(nav, navP);
            }

            var occ = GetLayerNumberForSpecialProperty(obj, "occlusion_layer");
            if (occ >= 0)
            {
                var occP = new OccluderPolygon2D();
                occP.Polygon = polygon;
                EnsureLayerExisting(LayerType.Occlusion, occ);
                currentTile.SetOccluder(occ, occP);
            }

            var phys = GetLayerNumberForSpecialProperty(obj, "physics_layer");
            // If no property is specified assume physics (i.e. default)
            if (phys < 0 && nav < 0 && occ < 0)
                phys = 0;
            if (phys < 0) continue;
            var polygonIndex = (int)polygonIndices.GetValueOrDefault(phys, 0);
            polygonIndices[phys] = polygonIndex + 1;
            EnsureLayerExisting(LayerType.Physics, phys);
            currentTile.AddCollisionPolygon(phys);
            currentTile.SetCollisionPolygonPoints(phys, polygonIndex, polygon);
            if (!obj.ContainsKey("properties")) continue;
            foreach (var property in (Array<Dictionary>)obj["properties"])
            {
                var name = (string)property.GetValueOrDefault("name", "");
                var type = (string)property.GetValueOrDefault("type", "string");
                var val = property.GetValueOrDefault("value", "");
                if (name == "") continue;
                switch (name.ToLower())
                {
                    case "one_way" when type == "bool":
                        currentTile.SetCollisionPolygonOneWay(phys, polygonIndex, bool.Parse((string)val));
                        break;
                    case "one_way_margin" when type == "int":
                        currentTile.SetCollisionPolygonOneWayMargin(phys, polygonIndex, int.Parse((string)val));
                        break;
                }
            }
        }
    }

    private Vector2 TransposeCoords(float x, float y)
    {
        if (_tilesetOrientation == "isometric")
        {
            var transX = (x - y) * _gridSize.X / _gridSize.Y / 2.0f;
            var transY = (x + y) * 0.5f;
            return new Vector2(transX, transY);
        }

        return new Vector2(x, y);
    }

    private static int GetLayerNumberForSpecialProperty(Dictionary dict, string propertyName)
    {
        if (!dict.ContainsKey("properties")) return -1;

        foreach (var property in (Array<Dictionary>)dict["properties"])
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = property.GetValueOrDefault("value", "");
            if (name == "") continue;
            if (name.ToLower() == propertyName && type == "int")
                return int.Parse((string)val);
        }
        return -1;
    }

    private Resource LoadResourceFromFile(string path)
    {
        var origPath = path;
        Resource ret = null;
        // ToDo: Not sure if this first check makes any sense since an image can't be imported properly if not in project tree
        if (!FileAccess.FileExists(path))
            path = _basePathTileset.GetBaseDir().PathJoin(origPath);
        if (!FileAccess.FileExists(path))
            path = _basePathTileset.PathJoin(origPath);
        if (FileAccess.FileExists(path))
            ret = ResourceLoader.Load(path);
        else
        {
            GD.PrintErr($"ERROR: Resource file '{origPath}' not found.");
            _errorCount++;
        }

        return ret;
    }
    
    private static uint GetBitmaskIntegerFromString(string maskString, int max)
    {
        uint ret = 0;
        var s1Arr = maskString.Split(',', StringSplitOptions.TrimEntries);
        foreach (var s1 in s1Arr)
        {
            if (s1.Contains('-'))
            {
                var s2Arr = s1.Split('-', 2, StringSplitOptions.TrimEntries);
                if (!int.TryParse(s2Arr[0], out var i1) || !int.TryParse(s2Arr[1], out var i2)) continue;
                if (i1 > i2) continue;
                for (var i = i1; i <= i2; i++)
                    if (i <= max)
                        ret += (uint)Math.Pow(2, i - 1);
            }
            else if (int.TryParse(s1, out var i)) 
                if (i <= max) 
                    ret += (uint)Math.Pow(2, i - 1);
        }

        return ret;
    }

    private static Variant GetRightTypedValue(string type, string val)
    {
        switch (type)
        {
            case "bool":
                return bool.Parse(val);
            case "float":
                return float.Parse(val, Inv);
            case "int":
                return int.Parse(val);
            case "color":
            {
                // If alpha is present it's strangely the first byte, so we have to shift it to the end
                if (val.Length == 9) val = val[0] + val[3..] + val.Substring(1, 2);
                return val;
            }
            default:
                return val;
        }
    }

    private void HandleTileProperties(Array<Dictionary> properties, TileData currentTile)
    {
        foreach (var property in properties)
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = (string)property.GetValueOrDefault("value", "");
            if (name == "") continue;
            if (name.ToLower() == "texture_origin_x" && type == "int")
            {
                var origin = currentTile.TextureOrigin;
                origin.X = int.Parse(val);
                currentTile.TextureOrigin = origin;
            }
            else if (name.ToLower() == "texture_origin_y" && type == "int")
            {
                var origin = currentTile.TextureOrigin;
                origin.Y = int.Parse(val);
                currentTile.TextureOrigin = origin;
            }
            else if (name.ToLower() == "modulate" && type == "string")
                currentTile.Modulate = new Color(val);
            else if (name.ToLower() == "material" && type == "file")
                currentTile.Material = (Material)LoadResourceFromFile(val);
            else if (name.ToLower() == "z_index" && type == "int")
                currentTile.ZIndex = int.Parse(val);
            else if (name.ToLower() == "y_sort_origin" && type == "int")
                currentTile.YSortOrigin = int.Parse(val);
            else if (name.ToLower() == "linear_velocity_x" && type is "int" or "float")
            {
                EnsureLayerExisting(LayerType.Physics, 0);
                var linVelo = currentTile.GetConstantLinearVelocity(0);
                linVelo.X = float.Parse(val, Inv);
                currentTile.SetConstantLinearVelocity(0, linVelo);
            }
            else if (name.ToLower().StartsWith("linear_velocity_x_") && type is "int" or "float")
            {
                if (!int.TryParse(name.AsSpan(18), out var layerIndex)) continue;
                EnsureLayerExisting(LayerType.Physics, layerIndex);
                var linVelo = currentTile.GetConstantLinearVelocity(layerIndex);
                linVelo.X = float.Parse(val, Inv);
                currentTile.SetConstantLinearVelocity(layerIndex, linVelo);
            }
            else if (name.ToLower() == "linear_velocity_y" && type is "int" or "float")
            {
                EnsureLayerExisting(LayerType.Physics, 0);
                var linVelo = currentTile.GetConstantLinearVelocity(0);
                linVelo.Y = float.Parse(val, Inv);
                currentTile.SetConstantLinearVelocity(0, linVelo);
            }
            else if (name.ToLower().StartsWith("linear_velocity_y_") && type is "int" or "float")
            {
                if (!int.TryParse(name.AsSpan(18), out var layerIndex)) continue;
                EnsureLayerExisting(LayerType.Physics, layerIndex);
                var linVelo = currentTile.GetConstantLinearVelocity(layerIndex);
                linVelo.Y = float.Parse(val, Inv);
                currentTile.SetConstantLinearVelocity(layerIndex, linVelo);
            }
            else if (name.ToLower() == "angular_velocity" && type is "int" or "float")
            {
                EnsureLayerExisting(LayerType.Physics, 0);
                currentTile.SetConstantAngularVelocity(0, float.Parse(val, Inv));
            }
            else if (name.ToLower().StartsWith("angular_velocity_") && type is "int" or "float")
            {
                if (!int.TryParse(name.AsSpan(17), out var layerIndex)) continue;
                EnsureLayerExisting(LayerType.Physics, layerIndex);
                currentTile.SetConstantAngularVelocity(layerIndex, float.Parse(val, Inv));
            }
            else
            {
                var customLayer = _tileset.GetCustomDataLayerByName(name);
                if (customLayer < 0)
                {
                    _tileset.AddCustomDataLayer();
                    customLayer = _tileset.GetCustomDataLayersCount() - 1;
                    _tileset.SetCustomDataLayerName(customLayer, name);
                    var customType = type switch
                    {
                        "bool" => Variant.Type.Bool,
                        "int" => Variant.Type.Int,
                        "string" => Variant.Type.String,
                        "float" => Variant.Type.Float,
                        "color" => Variant.Type.Color,
                        _ => Variant.Type.String
                    };
                    _tileset.SetCustomDataLayerType(customLayer, customType);
                }

                currentTile.SetCustomData(name, GetRightTypedValue(type, val));
            }
        }
    }

    private void HandleTilesetProperties(Array<Dictionary> properties)
    {
        foreach (var property in properties)
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = (string)property.GetValueOrDefault("value", "");
            if (name == "") continue;
            int layerIndex;
            if (name.ToLower() == "collision_layer" && type == "string")
            {
                EnsureLayerExisting(LayerType.Physics, 0);
                _tileset.SetPhysicsLayerCollisionLayer(0, GetBitmaskIntegerFromString(val, 32));
            }
            else if (name.ToLower().StartsWith("collision_layer_") && type == "string")
            {
                if (!int.TryParse(name.AsSpan(16), out layerIndex)) continue;
                EnsureLayerExisting(LayerType.Physics, layerIndex);
                _tileset.SetPhysicsLayerCollisionLayer(layerIndex, GetBitmaskIntegerFromString(val, 32));
            }
            else if (name.ToLower() == "collision_mask" && type == "string")
            {
                EnsureLayerExisting(LayerType.Physics, 0);
                _tileset.SetPhysicsLayerCollisionMask(0, GetBitmaskIntegerFromString(val, 32));
            }
            else if (name.ToLower().StartsWith("collision_mask_") && type == "string")
            {
                if (!int.TryParse(name.AsSpan(15), out layerIndex)) continue;
                EnsureLayerExisting(LayerType.Physics, layerIndex);
                _tileset.SetPhysicsLayerCollisionMask(layerIndex, GetBitmaskIntegerFromString(val, 32));
            }
            else if (name.ToLower() == "layers" && type == "string")
            {
                EnsureLayerExisting(LayerType.Navigation, 0);
                _tileset.SetNavigationLayerLayers(0, GetBitmaskIntegerFromString(val, 32));
            }
            else if (name.ToLower().StartsWith("layers_") && type == "string")
            {
                if (!int.TryParse(name.AsSpan(7), out layerIndex)) continue;
                EnsureLayerExisting(LayerType.Navigation, layerIndex);
                _tileset.SetNavigationLayerLayers(layerIndex, GetBitmaskIntegerFromString(val, 32));
            }
            else if (name.ToLower() == "light_mask" && type == "string")
            {
                EnsureLayerExisting(LayerType.Occlusion, 0);
                _tileset.SetOcclusionLayerLightMask(0, (int)GetBitmaskIntegerFromString(val, 20));
            }
            else if (name.ToLower().StartsWith("light_mask_") && type == "string")
            {
                if (!int.TryParse(name.AsSpan(11), out layerIndex)) continue;
                EnsureLayerExisting(LayerType.Occlusion, layerIndex);
                _tileset.SetOcclusionLayerLightMask(layerIndex, (int)GetBitmaskIntegerFromString(val, 20));
            }
            else if (name.ToLower() == "sdf_collision" && type == "bool")
            {
                EnsureLayerExisting(LayerType.Occlusion, 0);
                _tileset.SetOcclusionLayerSdfCollision(0, bool.Parse(val));
            }
            else if (name.ToLower().StartsWith("sdf_collision_") && type == "bool")
            {
                if (!int.TryParse(name.AsSpan(14), out layerIndex)) continue;
                EnsureLayerExisting(LayerType.Occlusion, layerIndex);
                _tileset.SetOcclusionLayerSdfCollision(layerIndex, bool.Parse(val));
            }
            else
                _tileset.SetMeta(name, GetRightTypedValue(type, val));
        }
    }

    private void EnsureLayerExisting(LayerType tp, int layer)
    {
        switch (tp)
        {
            case LayerType.Physics:
            {
                while (_physicsLayerCounter < layer)
                {
                    _tileset.AddPhysicsLayer();
                    _physicsLayerCounter++;
                }

                break;
            }
            case LayerType.Navigation:
            {
                while (_navigationLayerCounter < layer)
                {
                    _tileset.AddNavigationLayer();
                    _navigationLayerCounter++;
                }

                break;
            }
            case LayerType.Occlusion:
            {
                while (_occlusionLayerCounter < layer)
                {
                    _tileset.AddOcclusionLayer();
                    _occlusionLayerCounter++;
                }

                break;
            }
        }
    }

    private void HandleWangsetsOldMapping(Array<Dictionary> wangsets)
    {
        _tileset.AddTerrainSet();
        _terrainSetsCounter++;
        foreach (var wangset in wangsets)
        {
            var currentTerrainSet = _terrainSetsCounter;
            _tileset.AddTerrain(currentTerrainSet);
            var currentTerrain = _terrainCounter;
            if (wangset.TryGetValue("name", out var wsName))
                _tileset.SetTerrainName(currentTerrainSet, _terrainCounter, (string)wsName);

            var terrainMode = TileSet.TerrainMode.Corners;
            if (wangset.TryGetValue("type", out var wsType))
                terrainMode = (string)wsType switch
                {
                    "corner" => TileSet.TerrainMode.Corners,
                    "edge" => TileSet.TerrainMode.Sides,
                    "mixed" => TileSet.TerrainMode.CornersAndSides,
                    _ => terrainMode
                };

            _tileset.SetTerrainSetMode(currentTerrainSet, terrainMode);

            if (wangset.TryGetValue("colors", out var colors))
                _tileset.SetTerrainColor(currentTerrainSet, _terrainCounter,
                    new Color((Color)((Dictionary)((Array)colors)[0])["color"]));

            if (wangset.TryGetValue("wangtiles", out var wangtiles))
                foreach (var wangtile in (Array<Dictionary>)wangtiles)
                {
                    var tileId = (int)wangtile["tileid"];
                    var currentTile = CreateTileIfNotExistingAndGetTileData(tileId);
                    if (currentTile == null)
                        // Error occurred
                        break;

                    if (_tileSize.X != _mapTileSize.X || _tileSize.Y != _mapTileSize.Y)
                    {
                        var diffX = _tileSize.X - _mapTileSize.X;
                        if (diffX % 2 > 0)
                            diffX -= 1;
                        var diffY = _tileSize.Y - _mapTileSize.Y;
                        if (diffY % 2 > 0)
                            diffY += 1;
                        currentTile.TextureOrigin = new Vector2I(-diffX/2, diffY/2) - _tileOffset;
                    }

                    currentTile.TerrainSet = currentTerrainSet;
                    currentTile.Terrain = currentTerrain;
                    var i = 0;
                    foreach (var wi in (Array<int>)wangtile["wangid"])
                    {
                        var peeringBit = i switch
                        {
                            1 => TileSet.CellNeighbor.TopRightCorner,
                            2 => TileSet.CellNeighbor.RightSide,
                            3 => TileSet.CellNeighbor.BottomRightCorner,
                            4 => TileSet.CellNeighbor.BottomSide,
                            5 => TileSet.CellNeighbor.BottomLeftCorner,
                            6 => TileSet.CellNeighbor.LeftSide,
                            7 => TileSet.CellNeighbor.TopLeftCorner,
                            _ => TileSet.CellNeighbor.TopSide
                        };
                        if (wi > 0)
                            currentTile.SetTerrainPeeringBit(peeringBit, currentTerrain);
                        i++;
                    }
                }

            _terrainCounter++;
        }
    }

    private void HandleWangsets(Array<Dictionary> wangsets)
    {
        foreach (var wangset in wangsets)
        {
            _tileset.AddTerrainSet();
            _terrainSetsCounter++;
            _terrainCounter = -1;
            var currentTerrainSet = _terrainSetsCounter;
            var terrainSetName = "";
            if (wangset.TryGetValue("name", out var wsName))
                terrainSetName = (string)wsName;

            var terrainMode = TileSet.TerrainMode.Corners;
            if (wangset.TryGetValue("type", out var wsType))
                terrainMode = (string)wsType switch
                {
                    "corner" => TileSet.TerrainMode.Corners,
                    "edge" => TileSet.TerrainMode.Sides,
                    "mixed" => TileSet.TerrainMode.CornersAndSides,
                    _ => terrainMode
                };

            _tileset.SetTerrainSetMode(currentTerrainSet, terrainMode);

            if (wangset.TryGetValue("colors", out var colors))
                foreach (var wangcolor in (Array<Dictionary>) colors)
                {
                    _terrainCounter++;
                    _tileset.AddTerrain(currentTerrainSet);
                    _tileset.SetTerrainColor(currentTerrainSet, _terrainCounter, new Color((Color)wangcolor["color"]));
                    if (wangcolor.TryGetValue("name", out var colName))
                    {
                        if ((string)colName == "")
                            colName = terrainSetName;
                        _tileset.SetTerrainName(currentTerrainSet, _terrainCounter, (string)colName);
                    }
                    else
                        _tileset.SetTerrainName(currentTerrainSet, _terrainCounter, terrainSetName);                     
                }

            if (!wangset.TryGetValue("wangtiles", out var wangtiles)) continue;
            
            foreach (var wangtile in (Array<Dictionary>)wangtiles)
            {
                var tileId = (int)wangtile["tileid"];
                var currentTile = CreateTileIfNotExistingAndGetTileData(tileId);
                if (currentTile == null)
                    // Error occurred
                    break;

                if (_tileSize.X != _mapTileSize.X || _tileSize.Y != _mapTileSize.Y)
                {
                    var diffX = _tileSize.X - _mapTileSize.X;
                    if (diffX % 2 > 0)
                        diffX -= 1;
                    var diffY = _tileSize.Y - _mapTileSize.Y;
                    if (diffY % 2 > 0)
                        diffY += 1;
                    currentTile.TextureOrigin = new Vector2I(-diffX/2, diffY/2) - _tileOffset;
                }

                currentTile.TerrainSet = currentTerrainSet;
                var i = 0;
                foreach (var wi in (Array<int>)wangtile["wangid"])
                {
                    var peeringBit = i switch
                    {
                        1 => TileSet.CellNeighbor.TopRightCorner,
                        2 => TileSet.CellNeighbor.RightSide,
                        3 => TileSet.CellNeighbor.BottomRightCorner,
                        4 => TileSet.CellNeighbor.BottomSide,
                        5 => TileSet.CellNeighbor.BottomLeftCorner,
                        6 => TileSet.CellNeighbor.LeftSide,
                        7 => TileSet.CellNeighbor.TopLeftCorner,
                        _ => TileSet.CellNeighbor.TopSide
                    };
                    if (wi > 0)
                    {
                        currentTile.Terrain = wi-1;
                        currentTile.SetTerrainPeeringBit(peeringBit, wi-1);
                    }
                    i++;
                }
            }
        }
    }
}
