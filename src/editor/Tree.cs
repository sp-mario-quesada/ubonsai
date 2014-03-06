﻿#region License

/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2014 Vadim Macagon
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#endregion License

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UBonsai.Editor
{
    public class Tree
    {
        private Node _rootNode;
        private Vector2 _mousePosition;
        private List<Node> _selectedNodes = new List<Node>();
        private bool _allowMultiSelect = false;

        public void OnGUI(Event e)
        {
            _mousePosition = e.mousePosition;
            _allowMultiSelect = e.shift;

            if (_rootNode != null)
                _rootNode.OnGUI(e);

            switch (e.type)
            {
                case EventType.ContextClick:
                    OnContextClick();
                    e.Use();
                    break;

                case EventType.MouseDown:
                    ClearSelection();
                    e.Use();
                    break;
            }
        }

        private void OnContextClick()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Create Node"), false, CreateNode);
            menu.ShowAsContext();
        }

        private void CreateNode()
        {
            if (_rootNode == null)
            {
                _rootNode = new Node(_mousePosition.x, _mousePosition.y);
                _rootNode.NodeSelectionChanged += NodeSelectionChanged;
            }
        }

        private void ClearSelection()
        {
            // iterate in reverse order because when the selected status of a node changes
            // it'll fire off an event that will call NodeDeselected() to remove the node
            // from the list (removing from the back of the list will not invalidate the
            // iteration index)
            for (var i = _selectedNodes.Count - 1; i >= 0; i--)
            {
                _selectedNodes[i].Selected = false;
            }
        }

        private void NodeSelectionChanged(Node node)
        {
            if (node.Selected)
            {
                if (!_allowMultiSelect)
                {
                    ClearSelection();
                }
                _selectedNodes.Add(node);
            }
            else
                _selectedNodes.Remove(node);
        }
    }
}