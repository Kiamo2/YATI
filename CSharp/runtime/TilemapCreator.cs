// MIT License
//
// Copyright (c) 2024 Roland Helmerichs
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;
using FileAccess = Godot.FileAccess;

namespace YATI;

public class TilemapCreator
{
    private const uint FlippedHorizontallyFlag = 0x80000000;
    private const uint FlippedVerticallyFlag   = 0x40000000;
    private const uint FlippedDiagonallyFlag   = 0x20000000;

    private const string BackgroundColorRectName = "Background Color";
    private const string WarningColor = "Yellow";
    private const string CustomDataInternal = "__internal__";
    private const string GodotNodeTypeProperty = "godot_node_type";
    private const string GodotGroupProperty = "godot_group";
    private const string GodotScriptProperty = "godot_script";
    private const string DefaultAlignment = "unspecified";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private string _mapOrientation;
    private int _mapWidth;
    private int _mapHeight;
    private int _mapTileWidth;
    private int _mapTileHeight;
    private bool _infinite;
    //private int _parallaxOriginX;
    //private int _parallaxOriginY;
    private string _backgroundColor;
    
    private TileMapLayer _tilemapLayer;
    private TileSet _tileset;
    private string _currentTilesetOrientation;
    private string _currentObjectAlignment;
    private Node2D _baseNode;
    private ParallaxBackground _parallaxBackground;
    private ColorRect _background;
    private bool _parallaxLayerExisting;

    private string _basePath;
    private string _baseName;
    private string _encoding;
    private string _compression;
    private readonly Array<int> _firstGids = new ();
    private List<Dictionary> _atlasSources;
    private bool _useDefaultFilter;
    private bool _mapWangsetToTerrain;
    private bool _addClassAsMetadata;
    private bool _addIdAsMetadata;
    private bool _dontUseAlternativeTiles;
    private Dictionary _objectGroups;
    private CustomTypes _ct;

    private float _isoRot;
    private float _isoSkew;
    private Vector2 _isoScale;

    private int _errorCount;
    private int _warningCount;
    private int _godotVersion;

    private enum GodotType
    {
        Empty,
        Body,
        CBody,
        RBody,
        Area,
        Navigation,
        Occluder,
        Line,
        Path,
        Polygon,
        Instance,
        Unknown
    }

    public int GetErrorCount()
    {
        return _errorCount;
    }

    public int GetWarningCount()
    {
        return _warningCount;
    }
    
    public void SetUseDefaultFilter(bool value)
    {
        _useDefaultFilter = value;
    }

    public void SetAddClassAsMetadata(bool value)
    {
        _addClassAsMetadata = value;
    }

    public void SetAddIdAsMetadata(bool value)
    {
        _addIdAsMetadata = value;
    }

    public void SetNoAlternativeTiles(bool value)
    {
        _dontUseAlternativeTiles = value;
    }
    
    public void SetMapWangsetToTerrain(bool value)
    {
        _mapWangsetToTerrain = value;
    }

    public void SetCustomTypes(CustomTypes ct)
    {
        _ct = ct;
    }

    public TileSet GetTileset()
    {
        return _tileset;
    }

    private static void RecursivelyChangeOwner(Node node, Node newOwner)
    {
        if (node != newOwner)
            node.Owner = newOwner;
        if (node.GetChildCount() <= 0) return;
        foreach (var child in node.GetChildren())
            RecursivelyChangeOwner(child, newOwner);
    }

    public Node2D Create(string sourceFile)
    {
        _godotVersion = (int)Engine.GetVersionInfo()["hex"];
        _basePath = sourceFile.GetBaseDir();
        var baseDictionary = DictionaryBuilder.GetDictionary(sourceFile);
        _mapOrientation = (string)baseDictionary.GetValueOrDefault("orientation", "orthogonal");
        _mapWidth = (int)baseDictionary.GetValueOrDefault("width", 0);
        _mapHeight = (int)baseDictionary.GetValueOrDefault("height", 0);
        _mapTileWidth = (int)baseDictionary.GetValueOrDefault("tilewidth", 0);
        _mapTileHeight = (int)baseDictionary.GetValueOrDefault("tileheight", 0);
        _infinite = (bool)baseDictionary.GetValueOrDefault("infinite", false);
        //_parallaxOriginX = (int)baseDictionary.GetValueOrDefault("parallaxoriginx", 0);
        //_parallaxOriginY = (int)baseDictionary.GetValueOrDefault("parallaxoriginy", 0);
        _backgroundColor = (string)baseDictionary.GetValueOrDefault("backgroundcolor", "");

        _ct?.MergeCustomProperties(baseDictionary, "map");

        if (baseDictionary.TryGetValue("tilesets", out var tsVal))
        {
            var tileSets = (Array<Dictionary>)tsVal;
            foreach (var tileSet in tileSets)
                _firstGids.Add((int)tileSet["firstgid"]);
            var tilesetCreator = new TilesetCreator();
            tilesetCreator.SetBasePath(sourceFile);
            tilesetCreator.SetMapParameters(new Vector2I(_mapTileWidth, _mapTileHeight));
            if (_ct != null)
                tilesetCreator.SetCustomTypes(_ct);
            if (_mapWangsetToTerrain)
                tilesetCreator.MapWangsetToTerrain();
            _tileset = tilesetCreator.CreateFromDictionaryArray(tileSets);
            _errorCount = tilesetCreator.GetErrorCount();
            _warningCount = tilesetCreator.GetWarningCount();
            var unsorted = tilesetCreator.GetRegisteredAtlasSources();
            if (unsorted != null)
                _atlasSources = unsorted.OrderBy(x => (int)x["sourceId"]).ToList();
            _objectGroups = tilesetCreator.GetRegisteredObjectGroups();
        }
        // If tileset still null create an empty one
        _tileset ??= new TileSet();
        _tileset.TileSize = new Vector2I(_mapTileWidth, _mapTileHeight);
        switch (_mapOrientation)
        {
            case "isometric":
                _tileset.TileShape = TileSet.TileShapeEnum.Isometric;
                _tileset.TileLayout = TileSet.TileLayoutEnum.DiamondDown;
                break;
            case "staggered":
            {
                var staggerAxis = (string)baseDictionary.GetValueOrDefault("staggeraxis", "y");
                var staggerIndex = (string)baseDictionary.GetValueOrDefault("staggerindex", "odd");
                _tileset.TileShape = TileSet.TileShapeEnum.Isometric;
                _tileset.TileLayout = staggerIndex == "odd" ? TileSet.TileLayoutEnum.Stacked : TileSet.TileLayoutEnum.StackedOffset;
                _tileset.TileOffsetAxis = staggerAxis == "x" ? TileSet.TileOffsetAxisEnum.Vertical : TileSet.TileOffsetAxisEnum.Horizontal;
                break;
            }
            case "hexagonal":
            {
                var staggerAxis = (string)baseDictionary.GetValueOrDefault("staggeraxis", "y");
                var staggerIndex = (string)baseDictionary.GetValueOrDefault("staggerindex", "odd");
                _tileset.TileShape = TileSet.TileShapeEnum.Hexagon;
                _tileset.TileLayout = staggerIndex == "odd" ? TileSet.TileLayoutEnum.Stacked : TileSet.TileLayoutEnum.StackedOffset;
                _tileset.TileOffsetAxis = staggerAxis == "x" ? TileSet.TileOffsetAxisEnum.Vertical : TileSet.TileOffsetAxisEnum.Horizontal;
                break;
            }
        }
        
        _baseNode = new Node2D();
        _baseName = sourceFile.GetFile().GetBaseName();
        _baseNode.Name = _baseName;
        _parallaxBackground = new ParallaxBackground();
        _baseNode.AddChild(_parallaxBackground);
        _parallaxBackground.Name = _baseName + " (PBG)";
        _parallaxBackground.Owner = _baseNode;
        if (_backgroundColor != "")
        {
            _background = new ColorRect();
            _background.Color = new Color(_backgroundColor);
            _background.Size = new Vector2(_mapWidth * _mapTileWidth, _mapHeight * _mapTileHeight);
            _baseNode.AddChild(_background);
            _background.Name = BackgroundColorRectName;
            _background.Owner = _baseNode;
        }

        if (baseDictionary.TryGetValue("layers", out var layers))
            foreach (var layer in (Array<Dictionary>)layers)
                HandleLayer(layer, _baseNode);

        if (baseDictionary.TryGetValue("properties", out var mapProps))
            HandleProperties(_baseNode, (Array<Dictionary>)mapProps);
        
        if (_parallaxBackground.GetChildCount() == 0)
            _baseNode.RemoveChild(_parallaxBackground);
        
        // Remove internal helper custom data
        if (_tileset.GetCustomDataLayersCount() > 0)
            _tileset.RemoveCustomDataLayer(0);

        if (_baseNode.GetChildCount() > 1) return _baseNode;

        var ret = (Node2D)_baseNode.GetChild(0);
        RecursivelyChangeOwner(ret, ret);
        if (baseDictionary.TryGetValue("properties", out mapProps))
            HandleProperties(ret, (Array<Dictionary>)mapProps);
        ret.Name = _baseName;
        return ret;
    }
    
    private void HandleLayer(Dictionary layer, Node parent)
    {
        //var layerId = (int)layer["id"];
        var layerOffsetX = (float)layer.GetValueOrDefault("offsetx", 0);
        var layerOffsetY = (float)layer.GetValueOrDefault("offsety", 0);
        var layerOpacity = (float)layer.GetValueOrDefault("opacity", 1.0f);
        var layerVisible = (bool)layer.GetValueOrDefault("visible", true);
        _encoding = (string)layer.GetValueOrDefault("encoding", "csv");
        _compression = (string)layer.GetValueOrDefault("compression", "");
        var layertype = (string)layer.GetValueOrDefault("type", "tilelayer");
        var tintColor = (string)layer.GetValueOrDefault("tintcolor", "#ffffff");

        _ct?.MergeCustomProperties(layer, "layer");

        // v1.2: Skip layer
        if (GetProperty(layer, "no_import", "bool") == "true")
            return;

        switch (layertype)
        {
            case "tilelayer":
            {
                if (_mapOrientation == "isometric")
                    layerOffsetX += _mapTileWidth * (_mapHeight / 2.0f - 0.5f);
                var layerName = (string)layer["name"];

                _tilemapLayer = new TileMapLayer();
                if (layerName != "")
                    _tilemapLayer.Name = layerName;
                _tilemapLayer.Visible = layerVisible;
                _tilemapLayer.Position = new Vector2(layerOffsetX, layerOffsetY);
                if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                    _tilemapLayer.Modulate = new Color(tintColor, layerOpacity);
                _tilemapLayer.TileSet = _tileset;
                HandleParallaxes(parent, _tilemapLayer, layer);
                if (_mapOrientation is "isometric" or "staggered")
                    _tilemapLayer.YSortEnabled = true;
                
                if (!_useDefaultFilter)
                    _tilemapLayer.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

                if (_infinite && layer.TryGetValue("chunks", out var chunks))
                {
                    // Chunks
                    foreach (var chunk in (Array<Dictionary>)chunks)
                    {
                        var offsetX = (int)chunk["x"];
                        var offsetY = (int)chunk["y"];
                        var chunkWidth = (int)chunk["width"];
                        var chunkData = HandleData(chunk["data"]);
                        if (chunkData != null)
                            CreateMapFromData(chunkData, offsetX, offsetY, chunkWidth);
                    }
                }
                else if (layer.TryGetValue("data", out var dataVal))
                {
                    // Data
                    var data = HandleData(dataVal);
                    if (data != null)
                        CreateMapFromData(data, 0, 0, _mapWidth);
                }

                var classString = (string)layer.GetValueOrDefault("class", "");
                if (classString == "")
                    classString = (string)layer.GetValueOrDefault("type", "");
                if (_addClassAsMetadata && classString != "")
                    _tilemapLayer.SetMeta("class", classString);
                var objId = (int)layer.GetValueOrDefault("id", 0);
                if (_addIdAsMetadata && objId != 0)
                    _tilemapLayer.SetMeta("id", objId);

                if (layer.TryGetValue("properties", out var props))
                    HandleProperties(_tilemapLayer, (Array<Dictionary>)props);
              
                break;
            }
            case "objectgroup":
            {
                var layerNode = new Node2D();
                HandleParallaxes(parent, layerNode, layer);

                if (layer.TryGetValue("name", out var name))
                    layerNode.Name = (string)name;
                if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                    layerNode.Modulate = new Color(tintColor, layerOpacity);
                layerNode.Visible = (bool)layer.GetValueOrDefault("visible", true);
                var layerPosX = (float)layer.GetValueOrDefault("x", 0.0f);
                var layerPosY = (float)layer.GetValueOrDefault("y", 0.0f);
                layerNode.Position = new Vector2(layerPosX + layerOffsetX, layerPosY + layerOffsetY);
                if (!_useDefaultFilter)
                    layerNode.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
                if (_mapOrientation is "isometric" or "staggered")
                    layerNode.YSortEnabled = true;

                if (layer.TryGetValue("objects", out var objs))
                    foreach (var obj in (Array<Dictionary>)objs)
                        HandleObject(obj, layerNode, _tileset, Vector2.Zero);

                if (layer.TryGetValue("properties", out var props))
                    HandleProperties(layerNode, (Array<Dictionary>)props);
                
                break;
            }
            case "group":
            {
                var groupNode = new Node2D();
                HandleParallaxes(parent, groupNode, layer);
                groupNode.Name = (string)layer.GetValueOrDefault("name", "group");
                if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                    groupNode.Modulate = new Color(tintColor, layerOpacity);
                groupNode.Visible = (bool)layer.GetValueOrDefault("visible", true);
                var layerPosX = (float)layer.GetValueOrDefault("x", 0.0f);
                var layerPosY = (float)layer.GetValueOrDefault("y", 0.0f);
                groupNode.Position = new Vector2(layerPosX + layerOffsetX, layerPosY + layerOffsetY);
                // if (!_useDefaultFilter)
                //     groupNode.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

                foreach (Dictionary childLayer in (Array)layer["layers"])
                    HandleLayer(childLayer, groupNode);

                if (layer.TryGetValue("properties", out var props))
                    HandleProperties(groupNode, (Array<Dictionary>)props);
                
                break;
            }
            case "imagelayer":
            {
                var textureRect = new TextureRect();
                HandleParallaxes(parent, textureRect, layer);
                
                textureRect.Name = (string)layer.GetValueOrDefault("name", "image");
                textureRect.Position = new Vector2(layerOffsetX, layerOffsetY);

                var imagewidth = (int)layer.GetValueOrDefault("imagewidth", 0);
                var imageheight = (int)layer.GetValueOrDefault("imageheight", 0);
                textureRect.Size = new Vector2(imagewidth, imageheight);
                if ((layerOpacity < 1.0f) || (tintColor != "#ffffff"))
                    textureRect.Modulate = new Color(tintColor, layerOpacity);
                textureRect.Visible = layerVisible;

                // ToDo: Not sure if this first check makes any sense since an image can't be imported properly if not in project tree
                var texturePath = (string)layer["image"];
                if (!FileAccess.FileExists(texturePath))
                    texturePath = _basePath.GetBaseDir().PathJoin((string)layer["image"]);
                if (!FileAccess.FileExists(texturePath))
                    texturePath = _basePath.PathJoin((string)layer["image"]);
                if (FileAccess.FileExists(texturePath))
                {
                    var exists = ResourceLoader.Exists(texturePath, "Image");
                    if (exists)
                        textureRect.Texture = (Texture2D)ResourceLoader.Load(texturePath, "Image");
                    else
                    {
                        var image = Image.LoadFromFile(texturePath);
                        textureRect.Texture = ImageTexture.CreateFromImage(image);
                    }
                }
                else
                {
                    GD.PrintErr($"ERROR: Image file '{(string)layer["image"]}' not found.");
                    _errorCount++;
                }

                if (layer.TryGetValue("properties", out var props))
                    HandleProperties(textureRect, (Array<Dictionary>)props);
                
                break;
            }
        }
    }

    private void HandleParallaxes(Node parent, Node layerNode, Dictionary layerDict)
    {
        if (layerDict.ContainsKey("parallaxx") || layerDict.ContainsKey("parallaxy"))
        {
            if (!_parallaxLayerExisting)
            {
                if (_background != null)
                {
                    _background.Owner = null;
                    _background.Reparent(_parallaxBackground);
                    _background.Owner = _baseNode;
                }
                _parallaxLayerExisting = true;
            }

            var parX = (float)layerDict.GetValueOrDefault("parallaxx", 1.0f);
            var parY = (float)layerDict.GetValueOrDefault("parallaxy", 1.0f);
            var parallaxNode = new ParallaxLayer();
            _parallaxBackground.AddChild(parallaxNode);
            parallaxNode.Owner = _baseNode;
            var pxName = (string)layerDict.GetValueOrDefault("name", "");
            parallaxNode.Name = (pxName != "") ? pxName + " (PL)" : "ParallaxLayer";
            parallaxNode.MotionScale = new Vector2(parX, parY);
            parallaxNode.AddChild(layerNode);
        }
        else
            parent.AddChild(layerNode);

        layerNode.Owner = _baseNode;
    }
    
    private Array<uint> HandleData(Variant data)
    {
        var ret = new Array<uint>();
        switch (_encoding)
        {
            case "csv":
            {
                foreach (var cell in (Array)data)
                    ret.Add((uint)cell);
                break;
            }
            case "base64":
            {
                var bytes = Convert.FromBase64String((string)data);
                if (_compression != "")
                {
                    var memoryStream = new MemoryStream(bytes);
                    var memoryStreamOutput = new MemoryStream();
                    switch (_compression)
                    {
                        case "gzip":
                        {
                            var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
                            gZipStream.CopyTo(memoryStreamOutput);
                            break;
                        }
                        case "zlib":
                        {
                            var zLibStream = new ZLibStream(memoryStream, CompressionMode.Decompress);
                            zLibStream.CopyTo(memoryStreamOutput);
                            break;
                        }
                        default:
                            GD.PrintErr($"Decompression for type '{_compression}' not yet implemented.");
                            _errorCount++;
                            return null;
                    }
                    bytes = memoryStreamOutput.ToArray();
                }
                for (var i = 0; i < bytes.Length / sizeof(uint); i++)
                    ret.Add(BitConverter.ToUInt32(bytes, i * sizeof(uint)));
                break;
            }
        }

        return ret;
    }

    private void CreatePolygonsOnAlternativeTiles(TileData sourceData, TileData targetData, int altId)
    {
        var flippedH = (altId & 1) > 0;
        var flippedV = (altId & 2) > 0;
        var flippedD = (altId & 4) > 0;
        var origin = sourceData.TextureOrigin;
        var physicsLayersCount = _tileset.GetPhysicsLayersCount();
        for (var layerId = 0; layerId < physicsLayersCount; layerId++)
        {
            var collisionPolygonsCount = sourceData.GetCollisionPolygonsCount(layerId);
            for (var polygonId = 0; polygonId < collisionPolygonsCount; polygonId++)
            {
                var pts = sourceData.GetCollisionPolygonPoints(layerId, polygonId);
                var ptsNew = new Vector2[pts.Length];
                var i = 0;
                foreach (var pt in pts)
                {
                    ptsNew[i] = pt + origin;
                    if (flippedD)
                        (ptsNew[i].X, ptsNew[i].Y) = (ptsNew[i].Y, ptsNew[i].X);
                    if (flippedH)
                        ptsNew[i].X = -ptsNew[i].X;
                    if (flippedV)
                        ptsNew[i].Y = -ptsNew[i].Y;
                    ptsNew[i] -= targetData.TextureOrigin;
                    i++;
                }
                targetData.AddCollisionPolygon(layerId);
                targetData.SetCollisionPolygonPoints(layerId, polygonId, ptsNew);
            }
        }

        var navigationLayersCount = _tileset.GetNavigationLayersCount();
        for (var layerId = 0; layerId < navigationLayersCount; layerId++)
        {
            var navP = sourceData.GetNavigationPolygon(layerId);
            if (navP == null) continue;
            var pts = navP.GetOutline(0);
            var ptsNew = new Vector2[pts.Length];
            var i = 0;
            foreach (var pt in pts)
            {
                ptsNew[i] = pt + origin;
                if (flippedD)
                    (ptsNew[i].X, ptsNew[i].Y) = (ptsNew[i].Y, ptsNew[i].X);
                if (flippedH)
                    ptsNew[i].X = -ptsNew[i].X;
                if (flippedV)
                    ptsNew[i].Y = -ptsNew[i].Y;
                ptsNew[i] -= targetData.TextureOrigin;
                i++;
            }
            var navigationPolygon = new NavigationPolygon();
            navigationPolygon.AddOutline(ptsNew);
            //navigationPolygon.MakePolygonsFromOutlines();
            // Replaced in 4.2 deprecated function MakePolygonsFromOutlines
            navigationPolygon.Vertices = ptsNew;
            var polygon = new int[navigationPolygon.Vertices.Length];
            for (var idx = 0; idx < navigationPolygon.Vertices.Length; idx++)
                polygon[idx] = idx;
            navigationPolygon.AddPolygon(polygon);
            targetData.SetNavigationPolygon(layerId, navigationPolygon);
        }

        var occlusionLayersCount = _tileset.GetOcclusionLayersCount();
        for (var layerId = 0; layerId < occlusionLayersCount; layerId++)
        {
            var occ = sourceData.GetOccluder(layerId);
            if (occ == null) continue;
            var pts = occ.Polygon;
            var ptsNew = new Vector2[pts.Length];
            var i = 0;
            foreach (var pt in pts)
            {
                ptsNew[i] = pt + origin;
                if (flippedD)
                    (ptsNew[i].X, ptsNew[i].Y) = (ptsNew[i].Y, ptsNew[i].X);
                if (flippedH)
                    ptsNew[i].X = -ptsNew[i].X;
                if (flippedV)
                    ptsNew[i].Y = -ptsNew[i].Y;
                ptsNew[i] -= targetData.TextureOrigin;
                i++;
            }
            var occluderPolygon = new OccluderPolygon2D();
            occluderPolygon.Polygon = ptsNew;
            targetData.SetOccluder(layerId, occluderPolygon);
        }
    }

    private void CreateMapFromData(Array<uint> layerData, int offsetX, int offsetY, int mapWidth)
    {
        var cellCounter = -1;
        foreach (var cell in layerData)
        {
            cellCounter++;
            var intId = cell & 0xFFFFFFFF;
            var flippedH = (intId & FlippedHorizontallyFlag) > 0;
            var flippedV = (intId & FlippedVerticallyFlag) > 0;
            var flippedD = (intId & FlippedDiagonallyFlag) > 0;
            var gid = (int)(intId & 0x0FFFFFFF);
            if (gid <= 0) continue;
            var cellCoords = new Vector2I(cellCounter % mapWidth + offsetX, cellCounter / mapWidth + offsetY);

            var sourceId = GetMatchingSourceId(gid);
            // Should not be the case, but who knows...
            if (sourceId < 0) continue;
            var tileOffset = GetTileOffset(gid);

            TileSetAtlasSource atlasSource;
            if (_tileset.HasSource(sourceId))
                atlasSource = (TileSetAtlasSource)_tileset.GetSource(sourceId);
            else
                continue;
            var atlasWidth = atlasSource.GetAtlasGridSize().X;
            if (atlasWidth <= 0) continue;

            var effectiveGid = gid - _firstGids[GetFirstGidIndex(gid)];
            var atlasCoords = Vector2I.Zero;
            if (GetNumTilesForSourceId(sourceId) > 1)
                atlasCoords = atlasSource.GetAtlasGridSize() == Vector2I.One ? Vector2I.Zero : new Vector2I(effectiveGid % atlasWidth, effectiveGid / atlasWidth);
            if (!atlasSource.HasTile(atlasCoords))
            {
                atlasSource.CreateTile(atlasCoords);
                var currentTile = atlasSource.GetTileData(atlasCoords, 0);
                var tileSize = atlasSource.TextureRegionSize;
                if (tileSize.X != _mapTileWidth || tileSize.Y != _mapTileHeight)
                {
                    var diffX = tileSize.X - _mapTileWidth;
                    if (diffX % 2 > 0)
                        diffX -= 1;
                    var diffY = tileSize.Y - _mapTileHeight;
                    if (diffY % 2 > 0)
                        diffY += 1;
                    currentTile.TextureOrigin = new Vector2I(-diffX/2, diffY/2) - tileOffset;
                }
            }

            var altId = 0;
            if (flippedH || flippedV || flippedD)
            {
                if (_dontUseAlternativeTiles && _godotVersion >= 0x40200)
                {
                    if (flippedH)
                        altId |= (int)TileSetAtlasSource.TransformFlipH;
                    if (flippedV)
                        altId |= (int)TileSetAtlasSource.TransformFlipV;
                    if (flippedD)
                        altId |= (int)TileSetAtlasSource.TransformTranspose;
                }
                else
                {
                    altId = (flippedH ? 1 : 0) + (flippedV ? 2 : 0) + (flippedD ? 4 : 0);
                    if (!atlasSource.HasAlternativeTile(atlasCoords, altId))
                    {
                        atlasSource.CreateAlternativeTile(atlasCoords, altId);
                        var tileData = atlasSource.GetTileData(atlasCoords, altId);
                        tileData.FlipH = flippedH;
                        tileData.FlipV = flippedV;
                        tileData.Transpose = flippedD;
                        var tileSize = atlasSource.TextureRegionSize;
                        if (flippedD)
                            tileSize = new Vector2I(tileSize.Y, tileSize.X);
                        if (tileSize.X != _mapTileWidth || tileSize.Y != _mapTileHeight)
                        {
                            var diffX = tileSize.X - _mapTileWidth;
                            if (diffX % 2 != 0) 
                                diffX -= 1;
                            var diffY = tileSize.Y - _mapTileHeight;
                            if (diffY % 2 != 0)
                                diffY += 1;
                            tileData.TextureOrigin = new Vector2I(-diffX/2, diffY/2) - tileOffset;
                        }
                        CreatePolygonsOnAlternativeTiles(atlasSource.GetTileData(atlasCoords, 0), tileData, altId);
                    }
                }
            }
            _tilemapLayer.SetCell(cellCoords, sourceId, atlasCoords, altId);
        }
    }

    private static GodotType GetGodotType(string godotTypeString)
    {
        var gts = godotTypeString.ToLower();
        return gts switch
        {
            "" => GodotType.Empty,
            "collision" => GodotType.Body,
            "staticbody" => GodotType.Body,
            "characterbody" => GodotType.CBody,
            "rigidbody" => GodotType.RBody,
            "area" => GodotType.Area,
            "navigation" => GodotType.Navigation,
            "occluder" => GodotType.Occluder,
            "occlusion" => GodotType.Occluder,
            "line" => GodotType.Line,
            "path" => GodotType.Path,
            "polygon" => GodotType.Polygon,
            "instance" => GodotType.Instance,
            _ => GodotType.Unknown
        };
    }

    private static string GetGodotNodeTypeProperty(Dictionary obj, out bool propertyFound)
    {
        var ret = "";
        propertyFound = false;    
        if (!obj.TryGetValue("properties", out var props)) return ret;
        foreach (var property in (Array<Dictionary>)props)
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = (string)property.GetValueOrDefault("value", "");
            if (name.ToLower() != GodotNodeTypeProperty || type != "string") continue;
            propertyFound = true;
            ret = val;
            break;
        }

        return ret;
    }

    private static void SetSpriteOffset(Sprite2D objSprite, float width, float height, string alignment)
    {
        objSprite.Offset = alignment switch
        {
            "bottomleft" => new Vector2(width / 2.0f, -height / 2.0f),
            "bottom" => new Vector2(0.0f, -height / 2.0f),
            "bottomright" => new Vector2(-width / 2.0f, -height / 2.0f),
            "left" => new Vector2(width / 2.0f, 0.0f),
            "center" => new Vector2(0.0f, 0.0f),
            "right" => new Vector2(-width / 2.0f, 0.0f),
            "topleft" => new Vector2(width / 2.0f, height / 2.0f),
            "top" => new Vector2(0.0f, height / 2.0f),
            "topright" => new Vector2(-width / 2.0f, height / 2.0f),
            _ => objSprite.Offset
        };
    }

    private void HandleObject(Dictionary obj, Node layerNode, TileSet tileset, Vector2 offSet)
    {
        var objId = (int)obj.GetValueOrDefault("id", 0);
        var objX = (float)obj.GetValueOrDefault("x", offSet.X);
        var objY = (float)obj.GetValueOrDefault("y", offSet.Y);
        var objRot = (float)obj.GetValueOrDefault("rotation", 0.0f);
        var objWidth = (float)obj.GetValueOrDefault("width", 0.0f);
        var objHeight = (float)obj.GetValueOrDefault("height", 0.0f);
        var objVisible = (bool)obj.GetValueOrDefault("visible", true);
        var objName = (string)obj.GetValueOrDefault("name", "");

        _ct?.MergeCustomProperties(obj, "object");

        var classString = (string)obj.GetValueOrDefault("class", "");
        if (classString == "")
            classString = (string)obj.GetValueOrDefault("type", "");
        var godotNodeTypePropertyString = GetGodotNodeTypeProperty(obj, out var godotNodeTypePropFound);
        if (!godotNodeTypePropFound)
            godotNodeTypePropertyString = classString;
        var godotType = GetGodotType(godotNodeTypePropertyString);

        if (godotType == GodotType.Unknown)
        {
            if (!_addClassAsMetadata && classString != "" && !godotNodeTypePropFound)
            {
                GD.PrintRich($"[color={WarningColor}] -- Unknown class '{classString}'. -> Assuming Default[/color]");
                _warningCount++;
            }
            else if (godotNodeTypePropFound && godotNodeTypePropertyString != "")
            {
                GD.PrintRich($"[color={WarningColor}] -- Unknown {GodotNodeTypeProperty} '{godotNodeTypePropertyString}'. -> Assuming Default[/color]");
                _warningCount++;
            }
            godotType = GodotType.Body;
        }
        
        if (obj.TryGetValue("template", out var tplVal))
        {
            var templatePath = _basePath.PathJoin((string)tplVal);
            var templateDict = DictionaryBuilder.GetDictionary(templatePath);
            //var templateFirstGids = new Array<int>();
            TileSet templateTileSet = null;
            if (templateDict.TryGetValue("tilesets", out var tsVal))
            {
                var tileSets = (Array<Dictionary>)tsVal;
                //foreach (var tileSet in tileSets)
                //    templateFirstGids.Add((int)tileSet["firstgid"]);
                var tilesetCreator = new TilesetCreator();
                tilesetCreator.SetBasePath(templatePath);
                tilesetCreator.SetMapParameters(new Vector2I(_mapTileWidth, _mapTileHeight));
                if (_mapWangsetToTerrain)
                    tilesetCreator.MapWangsetToTerrain();
                templateTileSet = tilesetCreator.CreateFromDictionaryArray(tileSets);
                _errorCount += tilesetCreator.GetErrorCount();
                _warningCount += tilesetCreator.GetWarningCount();
            }

            if (templateDict.TryGetValue("objects", out var objs))
            {
                foreach (var templateObj in (Array<Dictionary>)objs)
                {
                    templateObj["template_dir_path"] = templatePath.GetBaseDir();
                    
                    // v1.5.3 Fix according to Carlo M (dogezen)
                    // override and merge properties defined in obj with properties defined in template
                    // since obj may override and define additional properties to those defined in template
                    if (obj.TryGetValue("properties", out var objProps))
                    {
                        if (templateObj.TryGetValue("properties", out var templateProps))
                        {
                            // merge obj properties that may have been overridden in the obj instance
                            // and add any additional properties defined in instanced obj that are 
                            // not defined in template
                            foreach (var prop in (Array<Dictionary>)objProps)
                            {
                                var found = false;
                                foreach (var templateProp in (Array<Dictionary>)templateProps)
                                {
                                    if ((string)prop["name"] != (string)templateProp["name"]) continue;
                                    templateProp["value"] = prop["value"];
                                    found = true;
                                    break;
                                }
                                if (!found)
                                    ((Array<Dictionary>)templateProps).Add(prop);
                            }
                        }
                        else
                        {
                            // template comes without properties, since obj has properties
                            // then merge them into the template
                            templateObj["properties"] = objProps;
                        }
                    }

                    HandleObject(templateObj, layerNode, templateTileSet, new Vector2(objX, objY));
                }
            }
        }
        
        // v1.2: New class 'instance'
        if (godotType == GodotType.Instance && !obj.ContainsKey("template") && !obj.ContainsKey("text"))
        {
            var resPath = GetProperty(obj, "res_path", "file");
            if (resPath == "")
            {
                GD.PrintErr("Object of class 'instance': Mandatory file property 'res_path' not found or invalid. -> Skipped");
                _errorCount++;
            }
            else
            {
                if (obj.TryGetValue("template_dir_path", out var tdPath))
                    resPath = ((string)tdPath).PathJoin(resPath);
                var scene = (PackedScene)LoadResourceFromFile(resPath);
                // Error check
                if (scene == null) return;

                var instance = scene.Instantiate();
                layerNode.AddChild(instance);
                instance.Owner = _baseNode;
                instance.Name = (objName != "") ? objName : resPath.GetFile().GetBaseName();
                ((Node2D)instance).Position = TransposeCoords(objX, objY);
                ((Node2D)instance).RotationDegrees = objRot;
                ((Node2D)instance).Visible = objVisible;
                if (_addClassAsMetadata && classString != "")
                    instance.SetMeta("class", classString);
                if (_addIdAsMetadata && objId != 0)
                    instance.SetMeta("id", objId);
                if (obj.TryGetValue("properties", out var props))
                    HandleProperties(instance, (Array<Dictionary>)props);
            }

            return;
        }

        if (obj.ContainsKey("gid"))
        {
            // gid refers to a tile in a tile set and object is created as sprite
            var intId = (uint)obj.GetValueOrDefault("gid", 0);
            intId &= 0xFFFFFFFF;
            var flippedH = (intId & FlippedHorizontallyFlag) > 0;
            var flippedV = (intId & FlippedVerticallyFlag) > 0;
            var gid = (int)(intId & 0x0FFFFFFF);
            
            var sourceId = GetMatchingSourceId(gid);
            // Should not be the case, but who knows...
            if (sourceId < 0) return;

            var tileOffset = GetTileOffset(gid);
            _currentTilesetOrientation = GetTilesetOrientation(gid);
            _currentObjectAlignment = GetTilesetAlignment(gid);
            if (_currentObjectAlignment == DefaultAlignment)
                _currentObjectAlignment = _mapOrientation == "orthogonal" ? "bottomleft" : "bottom";
        
            if (!tileset.HasSource(sourceId))
            {
                GD.PrintErr($"Could not get AtlasSource with id {sourceId}. -> Skipped");
                _errorCount++;
                return;
            }

            var gidSource = (TileSetAtlasSource)tileset.GetSource(sourceId);
            var objSprite = new Sprite2D();
            layerNode.AddChild(objSprite);
            objSprite.Owner = _baseNode;
            objSprite.Name = objName != "" ?
                objName : (gidSource.ResourceName != "" ?
                    gidSource.ResourceName : gidSource.Texture.ResourcePath.GetFile().GetBaseName() + "_tile");
            objSprite.Position = TransposeCoords(objX, objY) + tileOffset;
            objSprite.Texture = gidSource.Texture;
            objSprite.RotationDegrees = objRot;
            objSprite.Visible = objVisible;
            TileData td;
            if (GetNumTilesForSourceId(sourceId) > 1)
            {
                // Object is tile from partitioned tileset 
                var atlasWidth = gidSource.GetAtlasGridSize().X;

                // Can be zero, if tileset had an error
                if (atlasWidth <= 0) return;
          
                var effectiveGid = gid - _firstGids[GetFirstGidIndex(gid)];
                var atlasCoords = new Vector2I(effectiveGid % atlasWidth, effectiveGid / atlasWidth);
                if (!gidSource.HasTile(atlasCoords))
                    gidSource.CreateTile(atlasCoords);
                td = gidSource.GetTileData(atlasCoords, 0);
                objSprite.RegionEnabled = true;
                var regionSize = (Vector2)gidSource.TextureRegionSize;
                var pos = atlasCoords * regionSize;
                if (GetProperty(obj, "clip_artifacts", "bool") == "true")
                {
                    pos += new Vector2(0.5f, 0.5f);   
                    regionSize -= new Vector2(1.0f, 1.0f);
                    objWidth -= 1.0f;
                    objHeight -= 1.0f;
                }
                objSprite.RegionRect = new Rect2(pos, regionSize);
                SetSpriteOffset(objSprite, regionSize.X, regionSize.Y, _currentObjectAlignment);
                if (Math.Abs(regionSize.X - (int)objWidth) > 0.01f || Math.Abs(regionSize.Y - (int)objHeight) > 0.01f)
                {
                    var scaleX = objWidth / regionSize.X;
                    var scaleY = objHeight / regionSize.Y;
                    objSprite.Scale = new Vector2(scaleX, scaleY);
                }
            }
            else
            {
                // Object is single image tile
                var gidWidth = gidSource.TextureRegionSize.X;
                var gidHeight = gidSource.TextureRegionSize.Y;
                SetSpriteOffset(objSprite, gidWidth, gidHeight, _currentObjectAlignment);
                // Tiled sub rects?
                if (gidWidth != gidSource.Texture.GetWidth() || gidHeight != gidSource.Texture.GetHeight())
                {
                    objSprite.RegionEnabled = true;
                    objSprite.RegionRect = new Rect2(gidSource.Margins, gidSource.TextureRegionSize);
                }
                if ((gidWidth != (int)objWidth) || (gidHeight != (int)objHeight))
                {
                    var scaleX = objWidth / gidWidth;
                    var scaleY = objHeight / gidHeight;
                    objSprite.Scale = new Vector2(scaleX, scaleY);
                }

                td = gidSource.GetTileData(Vector2I.Zero, 0);
            }

            var idx = (int)td.GetCustomData(CustomDataInternal);
            if (idx > 0)
            {
                CollisionObject2D parent = godotType switch
                {
                    GodotType.Area => new Area2D(),
                    GodotType.CBody => new CharacterBody2D(),
                    GodotType.RBody => new RigidBody2D(),
                    GodotType.Body => new StaticBody2D(),
                    _ => null
                };
                if (parent != null)
                {
                    objSprite.Owner = null;
                    layerNode.RemoveChild(objSprite);
                    layerNode.AddChild(parent);
                    parent.Owner = _baseNode;
                    parent.Name = objSprite.Name;
                    parent.Position = objSprite.Position;
                    parent.RotationDegrees = objSprite.RotationDegrees;
                    objSprite.Position = Vector2.Zero;
                    objSprite.RotationDegrees = 0;
                    parent.AddChild(objSprite);
                    objSprite.Owner = _baseNode;
                    AddCollisionShapes(parent, GetObjectGroup(idx), objWidth, objHeight, flippedH, flippedV, objSprite.Scale);
                    if (obj.TryGetValue("properties", out var props))
                        HandleProperties(parent, (Array<Dictionary>)props);
                }
            }

            var metaList = td.GetMetaList();
            foreach (var metaName in metaList)
            {
                var metaVal = td.GetMeta(metaName);
                var metaType = metaVal.VariantType;
                var propDict = new Dictionary();
                propDict.Add("name", metaName);
                var propType = metaType switch
                {
                    Variant.Type.Bool => "bool",
                    Variant.Type.Int => "int",
                    Variant.Type.String => "string",
                    Variant.Type.Float => "float",
                    Variant.Type.Color => "color",
                    _ => "string"
                };
                // Type "file" assumed and thus forced for these properties 
                if (((string)metaName).ToLower() is "godot_script" or "material" or "physics_material_override")
                    propType = "file";
                
                propDict.Add("type", propType);
                propDict.Add("value", metaVal);
                
                if (obj.TryGetValue("properties", out var props2))
                {
                    // Add property only if not already contained in properties
                    var found = false;
                    foreach (var prop in (Array<Dictionary>)props2)
                    {
                        if (string.Equals((string)prop["name"], metaName, StringComparison.CurrentCultureIgnoreCase))
                            found = true;
                    }
                    if (!found)
                        ((Array<Dictionary>)props2).Add(propDict);
                }
                else
                {
                    var props = new Array<Dictionary> { propDict };
                    obj.Add("properties", props);
                }
            }

            objSprite.FlipH = flippedH;
            objSprite.FlipV = flippedV;

            if (_addClassAsMetadata && classString != "")
                objSprite.SetMeta("class", classString);
            if (_addIdAsMetadata && objId != 0)
                objSprite.SetMeta("id", objId);
            if (obj.TryGetValue("properties", out var props3))
                HandleProperties(objSprite, (Array<Dictionary>)props3);
        }
        else if (obj.TryGetValue("text", out var txtVal))
        {
            var objText = new Label();
            layerNode.AddChild(objText);
            objText.Owner = _baseNode;
            objText.Name = (objName != "") ? objName : "Text";
            objText.Position = TransposeCoords(objX, objY);
            objText.Size = new Vector2(objWidth, objHeight);
            objText.ClipText = true;
            objText.RotationDegrees = objRot;
            objText.Visible = objVisible;
            var txt = (Dictionary)txtVal;
            objText.Text = (string)txt.GetValueOrDefault("text", "Hello World");
            var wrap = (bool)txt.GetValueOrDefault("wrap", false);
            objText.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            var alignH = (string)txt.GetValueOrDefault("halign", "left");
            objText.HorizontalAlignment = alignH switch
            {
                "left" => HorizontalAlignment.Left,
                "center" => HorizontalAlignment.Center,
                "right" => HorizontalAlignment.Right,
                "justify" => HorizontalAlignment.Fill,
                _ => HorizontalAlignment.Left
            };
            var alignV = (string)txt.GetValueOrDefault("valign", "top");
            objText.VerticalAlignment = alignV switch
            {
                "top" => VerticalAlignment.Top,
                "center" => VerticalAlignment.Center,
                "bottom" => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top
            };
            var fontFamily = (string)txt.GetValueOrDefault("fontfamily", "Sans-Serif");
            var font = new SystemFont();
            font.FontNames = new[] { fontFamily };
            font.Oversampling = 5.0f;
            objText.AddThemeFontOverride("font", font);
            var fontSize = (int)txt.GetValueOrDefault("pixelsize", 16);
            objText.AddThemeFontSizeOverride("font_size", fontSize);
            var fontColor = (string)txt.GetValueOrDefault("color", "#000000");
            objText.AddThemeColorOverride("font_color", new Color(fontColor));
            if (obj.TryGetValue("properties", out var props))
                HandleProperties(objText, (Array<Dictionary>)props);
        }
        else if (!obj.ContainsKey("template"))
        {
            if (godotType == GodotType.Empty)
                godotType = GodotType.Body;
            
            var objectBaseCoords = TransposeCoords(objX, objY);

            if (obj.ContainsKey("point"))
            {
                var marker = new Marker2D();
                layerNode.AddChild(marker);
                marker.Owner = _baseNode;
                marker.Name = (objName != "") ? objName : "point";
                marker.Position = objectBaseCoords;
                marker.RotationDegrees = objRot;
                marker.Visible = objVisible;
                if (_addClassAsMetadata && classString != "")
                    marker.SetMeta("class", classString);
                if (_addIdAsMetadata && objId != 0)
                    marker.SetMeta("id", objId);
                if (obj.TryGetValue("properties", out var props))
                    HandleProperties(marker, (Array<Dictionary>)props);
            }
            else if (obj.ContainsKey("polygon"))
            {
                switch (godotType)
                {
                    case GodotType.Body or GodotType.Area:
                    {
                        CollisionObject2D co;
                        if (godotType == GodotType.Area)
                        {
                            co = new Area2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (Area)" : "Area";
                        }
                        else
                        {
                            co = new StaticBody2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (SB)" : "StaticBody";
                        }
                        co.Owner = _baseNode;
                        co.Position = objectBaseCoords;
                        co.Visible = objVisible;

                        var polygonShape = new CollisionPolygon2D();
                        polygonShape.Polygon = PolygonFromArray((Array<Dictionary>)obj["polygon"]);
                        co.AddChild(polygonShape);
                        polygonShape.Owner = _baseNode;
                        polygonShape.Name = (objName != "") ? objName : "Polygon Shape";
                        polygonShape.Position = Vector2.Zero;
                        polygonShape.RotationDegrees = objRot;
                        if (_addClassAsMetadata && classString != "")
                            co.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            co.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(co, (Array<Dictionary>)props);
                        break;
                    }
                    case GodotType.Navigation:
                    {
                        var navRegion = new NavigationRegion2D();
                        layerNode.AddChild(navRegion);
                        navRegion.Owner = _baseNode;
                        navRegion.Name = (objName != "") ? objName + " (NR)" : "Navigation";
                        navRegion.Position = objectBaseCoords;
                        navRegion.RotationDegrees = objRot;
                        navRegion.Visible = objVisible;

                        var navPoly = new NavigationPolygon();
                        navRegion.NavigationPolygon = navPoly;
                        var pg = PolygonFromArray((Array<Dictionary>)obj["polygon"]);
                        navPoly.AddOutline(pg);
                        //navPoly.MakePolygonsFromOutlines();
                        // Replaced in 4.2 deprecated function MakePolygonsFromOutlines
                        navPoly.Vertices = pg;
                        var polygon = new int[navPoly.Vertices.Length];
                        for (var idx = 0; idx < navPoly.Vertices.Length; idx++)
                            polygon[idx] = idx;
                        navPoly.AddPolygon(polygon);
                        
                        if (_addClassAsMetadata && classString != "")
                            navRegion.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            navRegion.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(navRegion, (Array<Dictionary>)props);
                        break;
                    }
                    case GodotType.Occluder:
                    {
                        var lightOcc = new LightOccluder2D();
                        layerNode.AddChild(lightOcc);
                        lightOcc.Owner = _baseNode;
                        lightOcc.Name = (objName != "") ? objName + " (LO)" : "Occluder";
                        lightOcc.Position = objectBaseCoords;
                        lightOcc.RotationDegrees = objRot;
                        lightOcc.Visible = objVisible;
                        
                        var occPoly = new OccluderPolygon2D();
                        lightOcc.Occluder = occPoly;
                        occPoly.Polygon = PolygonFromArray((Array<Dictionary>)obj["polygon"]);
                        if (_addClassAsMetadata && classString != "")
                            lightOcc.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            lightOcc.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(lightOcc, (Array<Dictionary>)props);
                        break;
                    }
                    case GodotType.Polygon:
                    {
                        var polygon = new Polygon2D();
                        layerNode.AddChild(polygon);
                        polygon.Owner = _baseNode;
                        polygon.Name = (objName != "") ? objName : "Polygon";
                        polygon.Position = objectBaseCoords;
                        polygon.RotationDegrees = objRot;
                        polygon.Visible = objVisible;
                        polygon.Polygon = PolygonFromArray((Array<Dictionary>)obj["polygon"]);
                        if (_addClassAsMetadata && classString != "")
                            polygon.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            polygon.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(polygon, (Array<Dictionary>)props);
                        break;
                    }
                }
            }
            else if (obj.ContainsKey("polyline"))
            {
                switch (godotType)
                {
                    case GodotType.Line:
                    {
                        var line = new Line2D();
                        layerNode.AddChild(line);
                        line.Owner = _baseNode;
                        line.Name = (objName != "") ? objName : "Line";
                        line.Position = objectBaseCoords;
                        line.Visible = objVisible;
                        line.RotationDegrees = objRot;
                        line.Width = 1.0f;
                    
                        line.Points = PolygonFromArray((Array<Dictionary>)obj["polyline"]);

                        if (_addClassAsMetadata && classString != "")
                            line.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            line.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(line, (Array<Dictionary>)props);
                        break;
                    }
                    case GodotType.Path:
                    {
                        var path = new Path2D();
                        layerNode.AddChild(path);
                        path.Owner = _baseNode;
                        path.Name = (objName != "") ? objName : "Path";
                        path.Position = objectBaseCoords;
                        path.Visible = objVisible;
                        path.RotationDegrees = objRot;

                        var curve = new Curve2D();
                        foreach (var point in (Array<Dictionary>)obj["polyline"])
                            curve.AddPoint(new Vector2((float)point["x"], (float)point["y"]));
                        path.Curve = curve;

                        if (_addClassAsMetadata && classString != "")
                            path.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            path.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(path, (Array<Dictionary>)props);
                        break;
                    }
                    default:
                    {
                        CollisionObject2D co;
                        if (godotType == GodotType.Area)
                        {
                            co = new Area2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (Area)" : "Area";
                        }
                        else
                        {
                            co = new StaticBody2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (SB)" : "StaticBody";
                        }
                        co.Owner = _baseNode;
                        co.Position = objectBaseCoords;
                        co.Visible = objVisible;

                        var polylinePoints = (Array<Dictionary>)obj["polyline"];
                        for (var i = 0; i < polylinePoints.Count - 1; i++)
                        {
                            var collisionShape = new CollisionShape2D();
                            co.AddChild(collisionShape);
                            var segmentShape = new SegmentShape2D();
                            segmentShape.A =
                                TransposeCoords((float)polylinePoints[i]["x"], (float)polylinePoints[i]["y"], true);
                            segmentShape.B = TransposeCoords((float)polylinePoints[i + 1]["x"],
                                (float)polylinePoints[i + 1]["y"], true);
                            collisionShape.Owner = _baseNode;
                            collisionShape.Shape = segmentShape;
                            collisionShape.Position = new Vector2(objWidth / 2.0f, objHeight / 2.0f);
                            collisionShape.RotationDegrees = objRot;
                            collisionShape.Name = "Segment Shape";
                        }

                        if (_addClassAsMetadata && classString != "")
                            co.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            co.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(co, (Array<Dictionary>)props);
                        break;
                    }
                }
            }
            else
            {
                switch (godotType)
                {
                    case GodotType.Body or GodotType.Area:
                    {
                        CollisionObject2D co;
                        if (godotType == GodotType.Area)
                        {
                            co = new Area2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (Area)" : "Area";
                        }
                        else
                        {
                            co = new StaticBody2D();
                            layerNode.AddChild(co);
                            co.Name= (objName != "") ? objName + " (SB)" : "StaticBody";
                        }
                        co.Owner = _baseNode;
                        co.Position = objectBaseCoords;
                        co.Visible = objVisible;

                        var collisionShape = new CollisionShape2D();
                        co.AddChild(collisionShape);
                        collisionShape.Owner = _baseNode;
                        if (obj.ContainsKey("ellipse"))
                        {
                            var capsuleShape = new CapsuleShape2D();
                            capsuleShape.Height = objHeight;
                            capsuleShape.Radius = objWidth / 2.0f;
                            collisionShape.Shape = capsuleShape;
                            collisionShape.Name = (objName != "") ? objName : "Capsule Shape";
                        }
                        else // Rectangle
                        {
                            var rectangleShape = new RectangleShape2D();
                            rectangleShape.Size = new Vector2(objWidth, objHeight);
                            collisionShape.Shape = rectangleShape;
                            collisionShape.Name = (objName != "") ? objName : "Rectangle Shape";
                        }

                        if (_mapOrientation == "isometric")
                        {
                            if (_isoRot == 0.0f)
                            {
                                var q = (float)_mapTileHeight / _mapTileWidth;
                                q *= q;
                                var cosA = Math.Sqrt(1 / (q + 1));
                                _isoRot = (float)(Math.Acos(cosA) * 180 / Math.PI);
                                _isoSkew = (float)((90 - 2 * _isoRot) * Math.PI / 180);
                                var scale = (float)(_mapTileWidth / (_mapTileHeight * 2 * cosA));
                                _isoScale = new Vector2(scale, scale);
                            }

                            collisionShape.Skew = _isoSkew;
                            collisionShape.Scale = _isoScale;
                            objRot += _isoRot;
                        }

                        collisionShape.Position = TransposeCoords(objWidth / 2.0f, objHeight / 2.0f, true);
                        collisionShape.RotationDegrees = objRot;
                        if (_addClassAsMetadata && classString != "")
                            co.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            co.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(co, (Array<Dictionary>)props);
                        break;
                    }
                    case GodotType.Navigation when obj.ContainsKey("ellipse"):
                        GD.PrintRich($"[color={WarningColor}] -- Ellipse is unusable for NavigationRegion2D. -> Skipped[/color]");
                        _warningCount++;
                        break;
                    case GodotType.Navigation:
                    {
                        var navRegion = new NavigationRegion2D();
                        layerNode.AddChild(navRegion);
                        navRegion.Owner = _baseNode;
                        navRegion.Name = (objName != "") ? objName + " (NR)" : "Navigation";
                        navRegion.Position = objectBaseCoords;
                        navRegion.RotationDegrees = objRot;
                        navRegion.Visible = objVisible;

                        var navPoly = new NavigationPolygon();
                        navRegion.NavigationPolygon = navPoly;
                        var pg = PolygonFromRectangle(objWidth, objHeight);
                        navPoly.AddOutline(pg);
                        //navPoly.MakePolygonsFromOutlines();
                        // Replaced in 4.2 deprecated function MakePolygonsFromOutlines
                        navPoly.Vertices = pg;
                        var polygon = new int[navPoly.Vertices.Length];
                        for (var idx = 0; idx < navPoly.Vertices.Length; idx++)
                            polygon[idx] = idx;
                        navPoly.AddPolygon(polygon);
                        if (_addClassAsMetadata && classString != "")
                            navRegion.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            navRegion.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(navRegion, (Array<Dictionary>)props);
                        break;
                    }
                    case GodotType.Occluder when obj.ContainsKey("ellipse"):
                        GD.PrintRich($"[color={WarningColor}] -- Ellipse is unusable for LightOccluder2D. -> Skipped[/color]");
                        _warningCount++;
                        break;
                    case GodotType.Occluder:
                    {
                        var lightOcc = new LightOccluder2D();
                        layerNode.AddChild(lightOcc);
                        lightOcc.Owner = _baseNode;
                        lightOcc.Name = (objName != "") ? objName + " (LO)" : "Occluder";
                        lightOcc.Position = objectBaseCoords;
                        lightOcc.RotationDegrees = objRot;
                        lightOcc.Visible = objVisible;
                        
                        var occPoly = new OccluderPolygon2D();
                        lightOcc.Occluder = occPoly;
                        occPoly.Polygon = PolygonFromRectangle(objWidth, objHeight);
                        if (_addClassAsMetadata && classString != "")
                            lightOcc.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            lightOcc.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(lightOcc, (Array<Dictionary>)props);
                        break;
                    }
                    case GodotType.Polygon when obj.ContainsKey("ellipse"):
                        GD.PrintRich($"[color={WarningColor}] -- Ellipse is unusable for Polygon2D. -> Skipped[/color]");
                        _warningCount++;
                        break;
                    case GodotType.Polygon:
                    {
                        var polygon = new Polygon2D();
                        layerNode.AddChild(polygon);
                        polygon.Owner = _baseNode;
                        polygon.Name = (objName != "") ? objName : "Polygon";
                        polygon.Position = objectBaseCoords;
                        polygon.RotationDegrees = objRot;
                        polygon.Visible = objVisible;
                        polygon.Polygon = PolygonFromRectangle(objWidth, objHeight);
                        
                        if (_addClassAsMetadata && classString != "")
                            polygon.SetMeta("class", classString);
                        if (_addIdAsMetadata && objId != 0)
                            polygon.SetMeta("id", objId);
                        if (obj.TryGetValue("properties", out var props))
                            HandleProperties(polygon, (Array<Dictionary>)props);
                        break;
                    }
                }
            }
        }
    }

    private void AddCollisionShapes(CollisionObject2D parent, Dictionary objectGroup, float tileWidth, float tileHeight, bool flippedH, bool flippedV, Vector2 scale)
    {
        var objects = (Array<Dictionary>)objectGroup["objects"];
        foreach (var obj in objects)
        {
            var objName = (string)obj.GetValueOrDefault("name", "");
            if (obj.ContainsKey("point") && (bool)obj["point"])
            {
                GD.PrintRich(
                    $"[color={WarningColor}] -- 'Point' has currently no corresponding collision element in Godot 4. -> Skipped[/color]");
                _warningCount++;
                break;
            }

            var fact = tileHeight / _mapTileHeight;
            var objectBaseCoords = new Vector2((float)obj["x"], (float)obj["y"]) * scale;
            if (_currentTilesetOrientation == "isometric")
            {
                objectBaseCoords = TransposeCoords((float)obj["x"], (float)obj["y"], true) * scale;
                tileWidth = _mapTileWidth;
                tileHeight = _mapTileHeight;
            }

            if (obj.TryGetValue("polygon", out var pts))
            {
                var polygonPoints = (Array<Dictionary>)pts;
                var rot = (float)obj.GetValueOrDefault("rotation", 0.0f);
                var polygon = new Vector2[polygonPoints.Count];
                var i = 0;
                foreach (var pt in polygonPoints)
                {
                    var pCoord = new Vector2((float)pt["x"], (float)pt["y"]) * scale;
                    if (_currentTilesetOrientation == "isometric")
                        pCoord = TransposeCoords(pCoord.X, pCoord.Y, true);
                    if (flippedH)
                        pCoord.X = -pCoord.X;
                    if (flippedV)
                        pCoord.Y = -pCoord.Y;
                    polygon[i] = pCoord;
                    i++;
                }

                var collisionPolygon = new CollisionPolygon2D();
                parent.AddChild(collisionPolygon);
                collisionPolygon.Owner = _baseNode;
                collisionPolygon.Polygon = polygon;
                var posX = objectBaseCoords.X;
                var posY = objectBaseCoords.Y - tileHeight;
                if (_mapOrientation == "isometric" && _currentTilesetOrientation == "orthogonal")
                    posX -= tileWidth / 2.0f;
                if (flippedH)
                {
                    posX = tileWidth - posX;
                    if (_mapOrientation == "isometric")
                        posX -= tileWidth;
                    rot = -rot;
                }

                if (flippedV)
                {
                    posY = -tileHeight - posY;
                    if (_currentTilesetOrientation == "isometric")
                        posY -= _mapTileHeight * fact - tileHeight;
                    rot = -rot;
                }
                collisionPolygon.RotationDegrees = rot;
                collisionPolygon.Position = new Vector2(posX, posY);
                collisionPolygon.Name = (objName != "") ? objName : "Collision Polygon";
                if (GetProperty(obj, "one_way", "bool") == "true")
                    collisionPolygon.OneWayCollision = true;
                var collMargin = GetProperty(obj, "one_way_margin", "int");
                if (collMargin == "")
                    collMargin = GetProperty(obj, "one_way_margin", "float");
                if (collMargin != "")
                    collisionPolygon.OneWayCollisionMargin = float.Parse(collMargin, Inv);
            }
            else
            {
                // Ellipse or rectangle
                var collisionShape = new CollisionShape2D();
                parent.AddChild(collisionShape);
                collisionShape.Owner = _baseNode;
                var x = (float)obj["x"] * scale.X;
                var y = (float)obj["y"] * scale.Y;
                var w = (float)obj["width"] * scale.X;
                var h = (float)obj["height"] * scale.Y;
                var rot = (float)obj.GetValueOrDefault("rotation", 0.0f);
                var sinA = (float)Math.Sin(rot * Math.PI / 180.0f);
                var cosA = (float)Math.Cos(rot * Math.PI / 180.0f);
                var posX = x + w / 2.0f * cosA - h / 2.0f * sinA;
                var posY = -tileHeight + y + h / 2.0f * cosA + w / 2.0f * sinA;
                if (_currentTilesetOrientation == "isometric")
                {
                    var transPos = TransposeCoords(posX, posY, true);
                    posX = transPos.X;
                    posY = transPos.Y;
                    posX -= tileWidth / 2.0f - h * fact / 4.0f * sinA;
                    posY -= tileHeight / 2.0f;
                }
                else if (_mapOrientation == "isometric")
                    posX -= tileWidth / 2.0f;

                if (flippedH)
                {                    
                    posX = tileWidth - posX;
                    if (_mapOrientation == "isometric")
                        posX -= tileWidth;
                    rot = -rot;
                }

                if (flippedV)
                {
                    posY = -tileHeight - posY;
                    if (_currentTilesetOrientation == "isometric")
                        posY -= _mapTileHeight * fact - tileHeight;
                    rot = -rot;
                }
                collisionShape.Position = new Vector2(posX, posY);
                collisionShape.Scale = scale;
                Shape2D shape;
                if (obj.ContainsKey("ellipse") && (bool)obj["ellipse"])
                {
                    shape = new CapsuleShape2D();
                    ((CapsuleShape2D)shape).Height = h / scale.Y;
                    ((CapsuleShape2D)shape).Radius = w / 2.0f / scale.X;
                    collisionShape.Name = (objName != "") ? objName : "Capsule Shape";
                }
                else
                {
                    shape = new RectangleShape2D();
                    ((RectangleShape2D)shape).Size = new Vector2(w,h) / scale;
                    collisionShape.Name = (objName != "") ? objName : "Rectangle Shape";
                }

                if (_currentTilesetOrientation == "isometric")
                {
                    if (_isoRot == 0.0f)
                    {
                        var q = (float)_mapTileHeight / _mapTileWidth;
                        q *= q;
                        var cosB = Math.Sqrt(1 / (q + 1));
                        _isoRot = (float)(Math.Acos(cosB) * 180 / Math.PI);
                        _isoSkew = (float)((90 - 2 * _isoRot) * Math.PI / 180);
                        var scaleB = (float)(_mapTileWidth / (_mapTileHeight * 2 * cosB));
                        _isoScale = new Vector2(scaleB, scaleB);
                    }

                    var effectiveRot = _isoRot;
                    var effectiveSkew = _isoSkew;
                    if (flippedH)
                    {
                        effectiveRot = -effectiveRot;
                        effectiveSkew = -effectiveSkew;
                    }
                    if (flippedV)
                    {
                        effectiveRot = -effectiveRot;
                        effectiveSkew = -effectiveSkew;
                    }

                    collisionShape.Skew = effectiveSkew;
                    collisionShape.Scale = _isoScale;
                    rot += effectiveRot;
                }
                
                collisionShape.Shape = shape;
                collisionShape.RotationDegrees = rot;
                if (GetProperty(obj, "one_way", "bool") == "true")
                    collisionShape.OneWayCollision = true;
                var collMargin = GetProperty(obj, "one_way_margin", "int");
                if (collMargin == "")
                    collMargin = GetProperty(obj, "one_way_margin", "float");
                if (collMargin != "")
                    collisionShape.OneWayCollisionMargin = float.Parse(collMargin, Inv);
            }
        }
    }

    private static string GetProperty(Dictionary obj, string propertyName, string propertyType)
    {
        const string ret = "";
        if (!obj.TryGetValue("properties", out var value)) return ret;
        foreach (var property in (Array<Dictionary>)value)
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = (string)property.GetValueOrDefault("value", "");
            if (name.ToLower() == propertyName && type == propertyType)
                return val;
        }

        return ret;
    }

    private Vector2[] PolygonFromArray(Array<Dictionary> polyArray)
    {
        var polygon = new Vector2[polyArray.Count];
        var i = 0;
        foreach (var pt in polyArray)
        {
            var pCoord = TransposeCoords((float)pt["x"], (float)pt["y"], true);
            polygon[i] = pCoord;
            i++;
        }
        
        return polygon;
    }
    
    private Vector2[] PolygonFromRectangle(float width, float height)
    {
        var polygon = new Vector2[4];
        polygon[0] = Vector2.Zero;
        polygon[1].X = polygon[0].X;
        polygon[1].Y = polygon[0].Y + height;
        polygon[2].X = polygon[0].X + width;
        polygon[2].Y = polygon[1].Y;
        polygon[3].X = polygon[2].X;
        polygon[3].Y = polygon[0].Y;
        polygon[1] = TransposeCoords(polygon[1].X, polygon[1].Y, true);
        polygon[2] = TransposeCoords(polygon[2].X, polygon[2].Y, true);
        polygon[3] = TransposeCoords(polygon[3].X, polygon[3].Y, true);
        return polygon;
    }

    private Vector2 TransposeCoords(float x, float y, bool noOffsetX = false)
    {
        if (_mapOrientation == "isometric")
        {
            var transX = (x - y) * _mapTileWidth / _mapTileHeight / 2.0f;
            if (!noOffsetX)
                transX += _mapHeight * _mapTileWidth / 2.0f;
            var transY = (x + y) * 0.5f;
            return new Vector2(transX, transY);
        }

        return new Vector2(x, y);
    }
    
    private int GetFirstGidIndex(int gid)
    {
        var index = 0;
        var gidIndex = 0;
        foreach (var firstgid in _firstGids)
        {
            if (gid >= firstgid)
                gidIndex = index;
            index++;
        }

        return gidIndex;
    }

    private int GetAtlasSourceIndex(int gid)
    {
        var idx = -1;
        if (_atlasSources == null)
            return -1;
        foreach (var src in _atlasSources)
        {
            idx++;
            var firstGid = (int)src["firstGid"];
            var effectiveGid = gid - firstGid + 1;
            var assignedId = (int)src["assignedId"];
            if (assignedId < 0)
            {
                var limit = (int)src["numTiles"];
                if (effectiveGid <= limit && firstGid == _firstGids[GetFirstGidIndex(gid)])
                    return idx;
            }
            else if (effectiveGid == (assignedId + 1)) 
                return idx;
        }
        return -1;
    }

    private int GetMatchingSourceId(int gid)
    {
        var idx = GetAtlasSourceIndex(gid);
        if (idx < 0)
            return -1;
        return (int)_atlasSources[idx]["sourceId"];
    }

    private Vector2I GetTileOffset(int gid)
    {
        var idx = GetAtlasSourceIndex(gid);
        if (idx < 0)
            return Vector2I.Zero;
        return (Vector2I)_atlasSources[idx]["tileOffset"];
    }

    private string GetTilesetOrientation(int gid)
    {
        var idx = GetAtlasSourceIndex(gid);
        if (idx < 0)
            return _mapOrientation;
        return (string)_atlasSources[idx]["tilesetOrientation"];
    }

    private string GetTilesetAlignment(int gid)
    {
        var idx = GetAtlasSourceIndex(gid);
        if (idx < 0)
            return DefaultAlignment;
        return (string)_atlasSources[idx]["objectAlignment"];
    }

    private int GetNumTilesForSourceId(int sourceId)
    {
        foreach (var src in _atlasSources.Where(src => (int)src["sourceId"] == sourceId))
            return (int)src["numTiles"];

        return -1;
    }

    private Resource LoadResourceFromFile(string path)
    {
        var origPath = path;
        Resource ret = null;
        // ToDo: Not sure if this first check makes any sense since an image can't be imported properly if not in project tree
        if (!FileAccess.FileExists(path))
            path = _basePath.GetBaseDir().PathJoin(origPath);
        if (!FileAccess.FileExists(path))
            path = _basePath.PathJoin(origPath);
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

    private Dictionary GetObjectGroup(int index)
    {
        Dictionary ret = null;
        if (_objectGroups != null)
            ret = (Dictionary)_objectGroups.GetValueOrDefault(index, (Dictionary)null);
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
                // If alpha is present it's oddly the first byte, so we have to shift it to the end
                if (val.Length == 9) val = val[0] + val[3..] + val.Substring(1, 2);
                return val;
            }
            default:
                return val;
        }
    }

    private void HandleProperties(Node targetNode, Array<Dictionary> properties)
    {
        var targetNodeClass = targetNode.GetType();
        var hasChildren = false;
        if (targetNodeClass == typeof(StaticBody2D) || targetNodeClass == typeof(Area2D) || 
            targetNodeClass == typeof(CharacterBody2D) || targetNodeClass == typeof(RigidBody2D))
            hasChildren = targetNode.GetChildCount() > 0;
        foreach (var property in properties)
        {
            var name = (string)property.GetValueOrDefault("name", "");
            var type = (string)property.GetValueOrDefault("type", "string");
            var val = (string)property.GetValueOrDefault("value", "");
            if (name == "" || name.ToLower() == GodotNodeTypeProperty) continue;
            if (name.StartsWith("__") && hasChildren)
            {
                var childPropDict = new Dictionary();
                childPropDict.Add("name", name[2..]);
                childPropDict.Add("type", type);
                childPropDict.Add("value", val);
                var childProps = new Array<Dictionary> { childPropDict };
                foreach (var child in targetNode.GetChildren())
                    HandleProperties(child, childProps);
            }

            switch (name.ToLower())
            {
                // Node properties
                // v1.5.4: godot_group property
                case GodotGroupProperty when (type == "string"):
                    foreach (var group in val.Split(','))
                        targetNode.AddToGroup(group.Trim(), true);
                    break;

                // v1.6.6: script resource and godot_script property
                case GodotScriptProperty when (type == "file"):
                    targetNode.SetScript((Script)ResourceLoader.Load(val, "Script"));
                    break;
                
                // CanvasItem properties
                case "modulate" when (type == "string"):
                    ((CanvasItem)targetNode).Modulate = new Color(val);
                    break;
                case "self_modulate" when (type == "string"):
                    ((CanvasItem)targetNode).SelfModulate = new Color(val);
                    break;
                case "show_behind_parent" when (type == "bool"):
                    ((CanvasItem)targetNode).ShowBehindParent = bool.Parse(val);
                    break;
                case "top_level" when (type == "bool"):
                    ((CanvasItem)targetNode).TopLevel = bool.Parse(val);
                    break;
                case "clip_children" when (type == "int"):
                    if (int.Parse(val) < (int)CanvasItem.ClipChildrenMode.Max)
                        ((CanvasItem)targetNode).ClipChildren = (CanvasItem.ClipChildrenMode)int.Parse(val);
                    break;
                case "light_mask" when (type == "string"):
                    ((CanvasItem)targetNode).LightMask = (int)GetBitmaskIntegerFromString(val, 20);
                    break;
                case "visibility_layer" when (type == "string"):
                    ((CanvasItem)targetNode).VisibilityLayer = GetBitmaskIntegerFromString(val, 20);
                    break;
                case "z_index" when (type == "int"):
                    ((CanvasItem)targetNode).ZIndex = int.Parse(val);
                    break;
                case "z_as_relative" when (type == "bool"):
                    ((CanvasItem)targetNode).ZAsRelative = bool.Parse(val);
                    break;
                case "y_sort_enabled" when (type == "bool"):
                    ((CanvasItem)targetNode).YSortEnabled = bool.Parse(val);
                    break;
                case "texture_filter" when (type == "int"):
                    if (int.Parse(val) < (int)CanvasItem.TextureFilterEnum.Max)
                        ((CanvasItem)targetNode).TextureFilter = (CanvasItem.TextureFilterEnum)int.Parse(val);
                    break;
                case "texture_repeat" when (type == "int"):
                    if (int.Parse(val) < (int)CanvasItem.TextureRepeatEnum.Max)
                        ((CanvasItem)targetNode).TextureRepeat = (CanvasItem.TextureRepeatEnum)int.Parse(val);
                    break;
                case "material" when (type == "file"):
                    ((CanvasItem)targetNode).Material = (Material)LoadResourceFromFile(val);
                    break;
                case "use_parent_material" when (type == "bool"):
                    ((CanvasItem)targetNode).UseParentMaterial = bool.Parse(val);
                    break;

                // TileMapLayer properties
                case "y_sort_origin" when type == "int" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    ((TileMapLayer)targetNode).YSortOrigin = int.Parse(val);
                    break;
                case "x_draw_order_reversed" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    ((TileMapLayer)targetNode).XDrawOrderReversed = bool.Parse(val);
                    break;
                case "rendering_quadrant_size" when type == "int" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    ((TileMapLayer)targetNode).RenderingQuadrantSize = int.Parse(val);
                    break;
                case "collision_enabled" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    ((TileMapLayer)targetNode).CollisionEnabled = bool.Parse(val);
                    break;
                case "use_kinematic_bodies" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    ((TileMapLayer)targetNode).UseKinematicBodies = bool.Parse(val);
                    break;
                case "collision_visibility_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    if (int.Parse(val) < 3)
                        ((TileMapLayer)targetNode).CollisionVisibilityMode = (TileMapLayer.DebugVisibilityMode)int.Parse(val);
                    break;
                case "navigation_enabled" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    ((TileMapLayer)targetNode).NavigationEnabled = bool.Parse(val);
                    break;
                case "navigation_visibility_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(TileMapLayer)):
                    if (int.Parse(val) < 3)
                        ((TileMapLayer)targetNode).NavigationVisibilityMode = (TileMapLayer.DebugVisibilityMode)int.Parse(val);
                    break;

                // CollisionObject2D properties
                case "disable_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    if (int.Parse(val) < 3)
                        ((CollisionObject2D)targetNode).DisableMode = (CollisionObject2D.DisableModeEnum)int.Parse(val);
                    break;
                case "collision_layer" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).CollisionLayer = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "collision_mask" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).CollisionMask = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "collision_priority" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).CollisionPriority = float.Parse(val, Inv);
                    break;
                case "input_pickable" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(CollisionObject2D)):
                    ((CollisionObject2D)targetNode).InputPickable = bool.Parse(val);
                    break;

                // CollisionPolygon2D properties
                case "build_mode" when type == "int" && hasChildren && int.Parse(val) < 2:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).BuildMode = (CollisionPolygon2D.BuildModeEnum)int.Parse(val);
                    break;

                // CollisionPolygon & CollisionShape2D properties
                case "disabled" when type == "bool" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).Disabled = bool.Parse(val);
                        else if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).Disabled = bool.Parse(val);
                    break;
                case "one_way_collision" when type == "bool" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).OneWayCollision = bool.Parse(val);
                        else if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).OneWayCollision = bool.Parse(val);
                    break;
                case "one_way_collision_margin" when type is "float" or "int" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionPolygon2D)))
                            ((CollisionPolygon2D)child).OneWayCollisionMargin = float.Parse(val, Inv);
                        else if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).OneWayCollisionMargin = float.Parse(val, Inv);
                    break;

                // CollisionShape2D properties
                case "debug_color" when type == "string" && hasChildren:
                    foreach (var child in targetNode.GetChildren())
                        if (child.GetType().IsAssignableTo(typeof(CollisionShape2D)))
                            ((CollisionShape2D)child).DebugColor = new Color(val);
                    break;
                
                // Area2D properties
                case "monitoring" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Monitoring = bool.Parse(val);
                    break;
                case "monitorable" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Monitorable = bool.Parse(val);
                    break;
                case "priority" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Priority = int.Parse(val, Inv);
                    break;
                case "gravity_space_override" when type == "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    if (int.Parse(val) < 5)
                        ((Area2D)targetNode).GravitySpaceOverride = (Area2D.SpaceOverride)int.Parse(val);
                    break;
                case "gravity_point" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPoint = bool.Parse(val);
                    break;
                case "gravity_point_center_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPointCenter = new Vector2(float.Parse(val, Inv), ((Area2D)targetNode).GravityPointCenter.Y);
                    break;
                case "gravity_point_center_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPointCenter = new Vector2(((Area2D)targetNode).GravityPointCenter.X, float.Parse(val, Inv));
                    break;
                case "gravity_point_unit_distance" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityPointUnitDistance = float.Parse(val, Inv);
                    break;
                case "gravity_direction_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityDirection = new Vector2(float.Parse(val, Inv), ((Area2D)targetNode).GravityDirection.Y);
                    break;
                case "gravity_direction_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).GravityDirection = new Vector2(((Area2D)targetNode).GravityDirection.X, float.Parse(val, Inv));
                    break;
                case "gravity" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).Gravity = float.Parse(val, Inv);
                    break;
                case "linear_damp_space_override" when type == "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    if (int.Parse(val) < 5)
                        ((Area2D)targetNode).LinearDampSpaceOverride = (Area2D.SpaceOverride)int.Parse(val);
                    break;
                case "linear_damp" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).LinearDamp = float.Parse(val, Inv);
                    break;
                case "angular_damp_space_override" when type == "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    if (int.Parse(val) < 5)
                        ((Area2D)targetNode).AngularDampSpaceOverride = (Area2D.SpaceOverride)int.Parse(val);
                    break;
                case "angular_damp" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Area2D)):
                    ((Area2D)targetNode).AngularDamp = float.Parse(val, Inv);
                    break;
                
                // StaticBody2D properties
                case "physics_material_override" when (type == "file"):
                    ((StaticBody2D)targetNode).PhysicsMaterialOverride = (PhysicsMaterial)LoadResourceFromFile(val);
                    break;
                case "constant_linear_velocity_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(StaticBody2D)):
                    ((StaticBody2D)targetNode).ConstantLinearVelocity = new Vector2(float.Parse(val, Inv), ((StaticBody2D)targetNode).ConstantLinearVelocity.Y);
                    break;
                case "constant_linear_velocity_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(StaticBody2D)):
                    ((StaticBody2D)targetNode).ConstantLinearVelocity = new Vector2(((StaticBody2D)targetNode).ConstantLinearVelocity.X, float.Parse(val, Inv));
                    break;
                case "constant_angular_velocity" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(StaticBody2D)):
                    ((StaticBody2D)targetNode).ConstantAngularVelocity = float.Parse(val, Inv);
                    break;
                
                // Character2D properties
                case "motion_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    if (int.Parse(val) < 2)
                        ((CharacterBody2D)targetNode).MotionMode = (CharacterBody2D.MotionModeEnum)int.Parse(val);
                    break;
                case "up_direction_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).UpDirection = new Vector2(float.Parse(val, Inv), ((CharacterBody2D)targetNode).UpDirection.Y);
                    break;
                case "up_direction_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).UpDirection = new Vector2(((CharacterBody2D)targetNode).UpDirection.X, float.Parse(val, Inv));
                    break;
                case "slide_on_ceiling" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).SlideOnCeiling = bool.Parse(val);
                    break;
                case "wall_min_slide_angle" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).WallMinSlideAngle = float.Parse(val, Inv);
                    break;
                case "floor_stop_on_slope" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).FloorStopOnSlope = bool.Parse(val);
                    break;
                case "floor_constant_speed" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).FloorConstantSpeed = bool.Parse(val);
                    break;
                case "floor_block_on_wall" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).FloorBlockOnWall = bool.Parse(val);
                    break;
                case "floor_max_angle" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).FloorMaxAngle = float.Parse(val, Inv);
                    break;
                case "floor_snap_length" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).FloorSnapLength = float.Parse(val, Inv);
                    break;
                case "platform_on_leave" when type == "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    if (int.Parse(val) < 3)
                        ((CharacterBody2D)targetNode).PlatformOnLeave = (CharacterBody2D.PlatformOnLeaveEnum)int.Parse(val);
                    break;
                case "platform_floor_layers" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).PlatformFloorLayers = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "platform_wall_layers" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).PlatformWallLayers = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "safe_margin" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).SafeMargin = float.Parse(val, Inv);
                    break;
                case "collision_layer" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).CollisionLayer = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "collision_mask" when type == "string" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).CollisionMask = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "collision_priority" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(CharacterBody2D)):
                    ((CharacterBody2D)targetNode).CollisionPriority = float.Parse(val, Inv);
                    break;
                
                // RigidBody2D properties
                case "mass" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).Mass = float.Parse(val, Inv);
                    break;
                case "inertia" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).Inertia = float.Parse(val, Inv);
                    break;
                case "center_of_mass" when type == "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    if (int.Parse(val) < 2)
                        ((RigidBody2D)targetNode).CenterOfMassMode = (RigidBody2D.CenterOfMassModeEnum)int.Parse(val);
                    break;
                case "physics_material_override" when (type == "file"):
                    ((StaticBody2D)targetNode).PhysicsMaterialOverride = (PhysicsMaterial)LoadResourceFromFile(val);
                    break;
                case "gravity_scale" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).GravityScale = float.Parse(val, Inv);
                    break;
                case "custom_integrator" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).CustomIntegrator = bool.Parse(val);
                    break;
                case "continuous_cd" when type == "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    if (int.Parse(val) < 3)
                        ((RigidBody2D)targetNode).ContinuousCd = (RigidBody2D.CcdMode)int.Parse(val);
                    break;
                case "max_contacts_reported" when type == "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).MaxContactsReported = int.Parse(val);
                    break;
                case "contact_monitor" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).ContactMonitor = bool.Parse(val);
                    break;
                case "sleeping" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).Sleeping = bool.Parse(val);
                    break;
                case "can_sleep" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).CanSleep = bool.Parse(val);
                    break;
                case "lock_rotation" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).LockRotation = bool.Parse(val);
                    break;
                case "freeze" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).Freeze = bool.Parse(val);
                    break;
                case "freeze_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    if (int.Parse(val) < 2)
                        ((RigidBody2D)targetNode).FreezeMode = (RigidBody2D.FreezeModeEnum)int.Parse(val);
                    break;
                case "linear_velocity_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).LinearVelocity = new Vector2(float.Parse(val, Inv), ((RigidBody2D)targetNode).LinearVelocity.Y);
                    break;
                case "linear_velocity_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).LinearVelocity = new Vector2(((RigidBody2D)targetNode).LinearVelocity.X, float.Parse(val, Inv));
                    break;
                case "linear_damp_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    if (int.Parse(val) < 2)
                        ((RigidBody2D)targetNode).LinearDampMode = (RigidBody2D.DampMode)int.Parse(val);
                    break;
                case "linear_damp" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).LinearDamp = float.Parse(val, Inv);
                    break;
                case "angular_velocity" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).AngularVelocity = float.Parse(val, Inv);
                    break;
                case "angular_damp_mode" when type == "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    if (int.Parse(val) < 2)
                        ((RigidBody2D)targetNode).AngularDampMode = (RigidBody2D.DampMode)int.Parse(val);
                    break;
                case "angular_damp" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).AngularDamp = float.Parse(val, Inv);
                    break;
                case "constant_force_x" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).ConstantForce = new Vector2(float.Parse(val, Inv), ((RigidBody2D)targetNode).ConstantForce.Y);
                    break;
                case "constant_force_y" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).ConstantForce = new Vector2(((RigidBody2D)targetNode).ConstantForce.X, float.Parse(val, Inv));
                    break;
                case "constant_torque" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(RigidBody2D)):
                    ((RigidBody2D)targetNode).ConstantTorque = float.Parse(val, Inv);
                    break;
                
                // NavigationRegion2D properties
                case "enabled" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).Enabled = bool.Parse(val);
                    break;
                case "navigation_layers" when type == "string" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).NavigationLayers = GetBitmaskIntegerFromString(val, 32);
                    break;
                case "enter_cost" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).EnterCost = float.Parse(val, Inv);
                    break;
                case "travel_cost" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(NavigationRegion2D)):
                    ((NavigationRegion2D)targetNode).TravelCost = float.Parse(val, Inv);
                    break;

                // LightOccluder2D properties
                case "sdf_collision" when type == "bool" && targetNodeClass.IsAssignableTo(typeof(LightOccluder2D)):
                    ((LightOccluder2D)targetNode).SdfCollision = bool.Parse(val);
                    break;
                case "occluder_light_mask" when type == "string" && targetNodeClass.IsAssignableTo(typeof(LightOccluder2D)):
                    ((LightOccluder2D)targetNode).OccluderLightMask = (int)GetBitmaskIntegerFromString(val, 20);
                    break;

                // Polygon2D properties
                case "color" when type == "string" && targetNodeClass.IsAssignableTo(typeof(Polygon2D)):
                    ((Polygon2D)targetNode).Color = new Color(val);
                    break;

                // Line2D properties
                case "width" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Line2D)):
                    ((Line2D)targetNode).Width = float.Parse(val, Inv);
                    break;
                case "default_color" when type == "string" && targetNodeClass.IsAssignableTo(typeof(Line2D)):
                    ((Line2D)targetNode).DefaultColor = new Color(val);
                    break;
                
                // Marker2D properties
                case "gizmo_extents" when type is "float" or "int" && targetNodeClass.IsAssignableTo(typeof(Line2D)):
                    ((Marker2D)targetNode).GizmoExtents = float.Parse(val, Inv);
                    break;
                
                // Other properties are added as Metadata
                default:
                {
                    targetNode.SetMeta(name, GetRightTypedValue(type, val));
                    break;
                }
            }
        }
    }
}
