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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UBonsai.Editor
{
    [Serializable]
    public class UndoRedoTracker : ScriptableObject
    {
        // tracks the undo stack size in CommandHistory
        public int index;

        public void OnEnable()
        {
            Debug.Log("UndoRedoTracker Enabled" + GetInstanceID());
            hideFlags = HideFlags.HideAndDontSave;
            name = "UndoRedoTracker";
        }
    }

    /// <summary>
    /// Provides undo/redo functionality via the Command pattern.
    /// </summary>
    public class CommandHistory : IDisposable
    {
        public ICommand[] UndoCommands
        {
            get { return _undoStack.ToArray(); }
        }

        public ICommand[] RedoCommands
        {
            get { return _redoStack.ToArray(); }
        }

        private bool _inUndoRedo = false;
        private Stack<ICommand> _undoStack = new Stack<ICommand>();
        private Stack<ICommand> _redoStack = new Stack<ICommand>();
        private UnityEditor.Undo.UndoRedoCallback _oldUndoRedoCallback = null;
        private UndoRedoTracker _tracker = null;

        public CommandHistory(bool hookIntoUnityEditorUndoSystem)
        {
            if (hookIntoUnityEditorUndoSystem)
                HookIntoUnityEditorUndoSystem();
        }

        /// <summary>
        /// Undo the last command that was executed or redone.
        /// </summary>
        public void Undo()
        {
            Undo(1);
        }

        /// <summary>
        /// Undo multiple commands.
        /// </summary>
        /// <param name="commandCount">Number of commands to undo.</param>
        public void Undo(int commandCount)
        {
            Debug.Log("Undo: " + commandCount);
            _inUndoRedo = true;
            for (int i = 0; (i < commandCount) && (_undoStack.Count > 0); i++)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
            }
            _inUndoRedo = false;
        }

        /// <summary>
        /// Redo the last command that was undone.
        /// </summary>
        public void Redo()
        {
            Redo(1);
        }

        /// <summary>
        /// Redo multiple commands.
        /// </summary>
        /// <param name="commandCount">Number of commands to redo.</param>
        public void Redo(int commandCount)
        {
            Debug.Log("Redo: " + commandCount);
            _inUndoRedo = true;
            for (int i = 0; (i < commandCount) && (_redoStack.Count > 0); i++)
            {
                var command = _redoStack.Pop();
                command.Redo();
                _undoStack.Push(command);
            }
            _inUndoRedo = false;
        }

        public void Execute(ICommand command)
        {
            Execute(command, false);
        }

        public void Execute(ICommand command, bool combine)
        {
            if (_inUndoRedo)
            {
                throw new InvalidOperationException("An undo/redo operation is in progress.");
            }

            _redoStack.Clear();
            command.Execute();
            if (combine && (_undoStack.Count > 0) && _undoStack.Peek().CombineWith(command))
            {
                // the previous command was combined with the current one so there's no
                // need to push the current command onto the undo stack
                return;
            }

            if (_tracker != null)
            {
                // when Unity calls UnityEditor.Undo.PerformUndo() the tracker index will
                // automatically revert back to the value set here
                _tracker.index = _undoStack.Count;
                UnityEditor.Undo.RecordObject(_tracker, command.Name);
            }

            _undoStack.Push(command);

            if (_tracker != null)
            {
                // when Unity calls UnityEditor.Undo.PerformRedo() the tracker index will
                // automatically revert back to the value set here
                _tracker.index = _undoStack.Count;
                Debug.Log("RecordObject Group ID: " + UnityEditor.Undo.GetCurrentGroup());
            }
        }

        private void HookIntoUnityEditorUndoSystem()
        {
            Debug.Log("Hooking into UnityEditor.Undo");
            _tracker = ScriptableObject.CreateInstance<UndoRedoTracker>();
            if (_tracker == null)
            {
                throw new NullReferenceException("CreateInstance<UndoRedoTracker>() failed!");
            }
            _oldUndoRedoCallback = UnityEditor.Undo.undoRedoPerformed;
            UnityEditor.Undo.undoRedoPerformed = UndoRedoPerformed;
        }

        private void UnhookFromUnityEditorUndoSystem()
        {
            Debug.Log("Unhooking from UnityEditor.Undo");
            if (_tracker != null)
            {
                UnityEditor.Undo.undoRedoPerformed = _oldUndoRedoCallback;
                _oldUndoRedoCallback = null;
                // FIXME: This doesn't work, lets hope it'll get fixed in the near future.
                UnityEditor.Undo.ClearUndo(_tracker);
                ScriptableObject.DestroyImmediate(_tracker);
                _tracker = null;
            }
            else
            {
                throw new InvalidOperationException("Not hooked into UnityEditor.Undo.");
            }
        }

        private void UndoRedoPerformed()
        {
            Debug.Log(
                "UndoRedoPerformed"
                + " Group ID: " + UnityEditor.Undo.GetCurrentGroup()
            );

            // play nicely with everyone else
            if (_oldUndoRedoCallback != null)
            {
                Debug.Log("Calling previous delegate.");
                _oldUndoRedoCallback();
            }

            // UnityEditor.Undo gives us just the one callback, figuring out whether we're getting
            // a callback because of an undo or a redo of an action we care about requires a bit of
            // trickiness. This is where the tracker object comes in, by detecting state changes in
            // the tracker object we can deduce what Unity just did. If the tracker index doesn't
            // match the undo stack size Unity must've performed an undo or a redo on it, otherwise
            // Unity just modified some other object's state (which we don't care about).
            if (_tracker.index < _undoStack.Count)
            {
                Undo();
            }
            else if (_tracker.index > _undoStack.Count)
            {
                Redo();
            }
        }

        public void Dispose()
        {
            if (_tracker != null)
                UnhookFromUnityEditorUndoSystem();
        }
    }
}