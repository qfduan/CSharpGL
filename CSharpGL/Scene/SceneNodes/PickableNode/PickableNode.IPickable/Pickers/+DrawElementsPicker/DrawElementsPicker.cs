﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CSharpGL
{
    /// <summary>
    /// Get picked geometry from a <see cref="PickableNode"/> with <see cref="DrawArraysCmd"/> as index buffer.
    /// </summary>
    partial class DrawElementsPicker : PickerBase
    {
        /// <summary>
        /// 
        /// </summary>
        public DrawElementsCmd DrawCommand { get; private set; }

        /// <summary>
        /// Get picked geometry from a <see cref="PickableNode"/> with <see cref="DrawArraysCmd"/> as index buffer.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="positionBuffer"></param>
        /// <param name="drawCommand"></param>
        public DrawElementsPicker(PickableNode node, VertexBuffer positionBuffer, DrawElementsCmd drawCommand)
            : base(node, positionBuffer)
        {
            this.DrawCommand = drawCommand;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="stageVertexId">The last vertex's id that constructs the picked primitive.
        /// <para>This id is in scene's all <see cref="IPickable"/>s' order.</para></param>
        /// <param name="baseId">Index of first vertex of the buffer that The geometry belongs to.
        /// <para>This id is in scene's all <see cref="IPickable"/>s' order.</para></param>
        /// <returns></returns>
        public override PickedGeometry GetPickedGeometry(PickingEventArgs arg, uint stageVertexId, uint baseId)
        {
            if (stageVertexId < baseId) { return null; }
            uint flatColorVertexId = stageVertexId - baseId;
            if (this.PositionBuffer.Length <= flatColorVertexId) { return null; }

            PickableNode node = this.Node;

            // Find primitiveInfo
            RecognizedPrimitiveInfo primitiveInfo = this.GetPrimitiveInfoOfPickedGeometry(arg, flatColorVertexId);
            if (primitiveInfo == null)
            {
                Debug.WriteLine(string.Format(
                    "Got flatColorVertexId[{0}] but no primitiveInfo! Params are [{1}] [{2}]",
                    flatColorVertexId, arg, stageVertexId));
                { return null; }
            }

            PickingGeometryTypes geometryType = arg.GeometryType;
            DrawMode drawMode = this.DrawCommand.CurrentMode;
            GeometryType typeOfMode = drawMode.ToGeometryType();

            if ((geometryType & PickingGeometryTypes.Point) == PickingGeometryTypes.Point)
            {
                // 获取pickedGeometry
                if (typeOfMode == GeometryType.Point)
                { return PickWhateverItIs(arg, stageVertexId, primitiveInfo, typeOfMode); }
                else if (typeOfMode == GeometryType.Line)
                {
                    if (this.OnPrimitiveTest(flatColorVertexId, drawMode))
                    { return PickPoint(arg, stageVertexId, flatColorVertexId); }
                    else
                    { return null; }
                }
                else
                {
                    DrawElementsPointSearcher searcher = GetPointSearcher(drawMode);
                    if (searcher != null)// line is from triangle, quad or polygon
                    { return SearchPoint(arg, stageVertexId, flatColorVertexId, primitiveInfo, searcher); }
                    else
                    { throw new Exception(string.Format("Lack of searcher for [{0}]", drawMode)); }
                }
            }
            else if ((geometryType & PickingGeometryTypes.Line) == PickingGeometryTypes.Line)
            {
                // 获取pickedGeometry
                if (typeOfMode == GeometryType.Point) // want a line when rendering GL_POINTS
                { return null; }
                if (typeOfMode == GeometryType.Line)
                { return PickWhateverItIs(arg, stageVertexId, primitiveInfo, typeOfMode); }
                else
                {
                    DrawElementsLineSearcher searcher = GetLineSearcher(drawMode);
                    if (searcher != null)// line is from triangle, quad or polygon
                    { return SearchLine(arg, stageVertexId, primitiveInfo, searcher); }
                    else
                    { throw new Exception(string.Format("Lack of searcher for [{0}]", drawMode)); }
                }
            }
            else
            {
                if (geometryType.Contains(typeOfMode)) // I want what it is
                { return PickWhateverItIs(arg, stageVertexId, primitiveInfo, typeOfMode); }
                else
                { return null; }
                //{ throw new Exception(string.Format("Lack of searcher for [{0}]", mode)); }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="stageVertexId"></param>
        /// <param name="flatColorVertexId"></param>
        /// <param name="primitiveInfo"></param>
        /// <param name="searcher"></param>
        /// <returns></returns>
        private PickedGeometry SearchPoint(PickingEventArgs arg, uint stageVertexId, uint flatColorVertexId, RecognizedPrimitiveInfo primitiveInfo, DrawElementsPointSearcher searcher)
        {
            var vertexIds = new uint[] { searcher.Search(arg, primitiveInfo, this), };
            vec3[] positions = FillPickedGeometrysPosition(vertexIds);
            var pickedGeometry = new PickedGeometry(GeometryType.Point, positions, vertexIds, stageVertexId, this.Node);

            return pickedGeometry;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="stageVertexId"></param>
        /// <param name="primitiveInfo"></param>
        /// <param name="searcher"></param>
        /// <returns></returns>
        private PickedGeometry SearchLine(PickingEventArgs arg, uint stageVertexId, RecognizedPrimitiveInfo primitiveInfo, DrawElementsLineSearcher searcher)
        {
            var vertexIds = searcher.Search(arg, primitiveInfo, this);
            vec3[] positions = FillPickedGeometrysPosition(vertexIds);
            var pickedGeometry = new PickedGeometry(GeometryType.Line, positions, vertexIds, stageVertexId, this.Node);

            return pickedGeometry;
        }

        /// <summary>
        /// 是三角形，就pick一个三角形；是四边形，就pick一个四边形，是多边形，就pick一个多边形。
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="stageVertexId"></param>
        /// <param name="primitiveInfo"></param>
        /// <param name="typeOfMode"></param>
        /// <returns></returns>
        private PickedGeometry PickWhateverItIs(PickingEventArgs arg, uint stageVertexId, RecognizedPrimitiveInfo primitiveInfo, GeometryType typeOfMode)
        {
            uint[] vertexIds = primitiveInfo.VertexIds;
            vec3[] positions = FillPickedGeometrysPosition(vertexIds);
            var pickedGeometry = new PickedGeometry(typeOfMode, positions, vertexIds, stageVertexId, this.Node);

            return pickedGeometry;
        }

        /// <summary>
        /// I don't know how to implement this method in a high effitiency way.
        /// So keep it like this.
        /// Also, why would someone use glDrawElements() when rendering GL_POINTS?
        /// </summary>
        /// <param name="flatColorVertexId"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        private bool OnPrimitiveTest(uint flatColorVertexId, DrawMode mode)
        {
            return true;
        }

        private PickedGeometry PickPoint(PickingEventArgs arg, uint stageVertexId, uint flatColorVertexId)
        {
            var vertexIds = new uint[] { flatColorVertexId, };
            vec3[] positions = FillPickedGeometrysPosition(vertexIds);
            var pickedGeometry = new PickedGeometry(GeometryType.Point, positions, vertexIds, stageVertexId, this.Node);

            return pickedGeometry;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="flatColorVertexId"></param>
        /// <returns></returns>
        private RecognizedPrimitiveInfo GetPrimitiveInfoOfPickedGeometry(
            PickingEventArgs arg,
            uint flatColorVertexId)
        {
            List<RecognizedPrimitiveInfo> primitiveInfoList = GetPossiblePrimitives(arg, flatColorVertexId);

            if (primitiveInfoList.Count == 0) { return null; }

            RecognizedPrimitiveInfo primitiveInfo = FindThePickedOne(arg, primitiveInfoList);

            return primitiveInfo;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="twoPrimitivesIndexBuffer"></param>
        /// <returns></returns>
        private uint Pick(PickingEventArgs arg, DrawElementsCmd twoPrimitivesIndexBuffer)
        {
            this.Node.Render4InnerPicking(arg, twoPrimitivesIndexBuffer);

            uint pickedIndex = ColorCodedPicking.ReadStageVertexId(arg.X, arg.Y);

            return pickedIndex;
        }

        /// <summary>
        /// 遍历以<paramref name="flatColorVertexId"/>为最后一个顶点的图元，
        /// 瞄准每个图元的索引（例如1个三角形有3个索引）中的最后一个索引，
        /// 将此索引在<see cref="IndexBuffer"/>中的索引（位置）收集起来。
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="flatColorVertexId"></param>
        /// <returns></returns>
        private List<RecognizedPrimitiveInfo> GetPossiblePrimitives(PickingEventArgs arg, uint flatColorVertexId)
        {
            var drawCmd = this.DrawCommand;
            DrawMode mode = drawCmd.CurrentMode;
            PrimitiveRecognizer recognizer = PrimitiveRecognizerFactory.Create(
                (arg.GeometryType.Contains(GeometryType.Point)
                && mode.ToGeometryType() == GeometryType.Line) ?
                DrawMode.Points : mode);

            List<RecognizedPrimitiveInfo> primitiveInfoList = recognizer.Recognize(flatColorVertexId, drawCmd);
            return primitiveInfoList;
        }

    }
}
