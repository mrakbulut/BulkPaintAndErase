using System;
using UnityEngine;
using XDPaint.Utils;
using IDisposable = XDPaint.Core.IDisposable;

namespace XDPaint.States
{
#if UNITY_EDITOR && XDP_DEBUG
    [Serializable]
#endif
    public class RenderTextureChangeRecord : BaseChangeRecord, IDisposable
    {
        public Action OnAction;
        public Action<RenderTexture> OnClearTexture;

        public RenderTexture RenderTexture => newValue;
        public RenderTexture NewTexture => newTexture;
        public Texture OldTexture => oldTexture;
        public object Entity => changedObject;
        public string Property => propertyName;
        
        private RenderTexture newValue;
        private RenderTexture oldValue;
        private Texture oldTexture;
        private RenderTexture newTexture;
        private object changedObject;
        private string propertyName;

        public RenderTextureChangeRecord(object entity, string property, RenderTexture oldValue, RenderTexture newValue, Texture oldTexture)
        {
            changedObject = entity;
            propertyName = property;
            this.oldValue = oldValue;
            this.newValue = newValue;
            this.oldTexture = oldTexture;
            
            if (this.newValue != null)
            {
                newTexture = RenderTextureFactory.CreateTemporaryRenderTexture(this.newValue);
                Graphics.Blit(this.newValue, newTexture);
            }
        }

        public override void Undo()
        {
            var property = changedObject.GetType().GetProperty(propertyName);
            if (property.GetCustomAttributes(typeof(UndoRedoAttribute), true).Length == 0)
                return;

            if (property.CanWrite)
            {
                property.SetValue(changedObject, oldValue, null);
            }
            
            UpdateTexture(oldTexture);
            OnAction?.Invoke();
        }

        public override void Redo()
        {
            var property = changedObject.GetType().GetProperty(propertyName);
            if (property.GetCustomAttributes(typeof(UndoRedoAttribute), true).Length == 0) 
                return;
            
            if (property.CanWrite)
            {
                property.SetValue(changedObject, newValue, null);
            }
            
            UpdateTexture(newTexture);
            OnAction?.Invoke();
        }

        public void SetNewTexture(RenderTexture texture)
        {
            newTexture = texture;
        }

        private void UpdateTexture(Texture texture)
        {
            if (texture != null && newValue != null)
            {
                Graphics.Blit(texture, newValue);
            }
            else if (texture == null && newValue != null)
            {
                OnClearTexture?.Invoke(newValue);
            }
            else if (texture is RenderTexture renderTexture)
            {
                newValue = renderTexture;
            }
        }

        public void ReleaseOldTexture()
        {
            if (oldTexture != null && oldTexture is RenderTexture r)
            {
                RenderTexture.ReleaseTemporary(r);
                oldTexture = null;
            }
        }

        public void ReleaseRenderTexture()
        {
            if (newValue != null && newValue is RenderTexture r)
            {
                r.ReleaseTexture();
                newValue = null;
            }
        }

        public void DoDispose()
        {
            if (newTexture != null)
            {
                RenderTexture.ReleaseTemporary(newTexture);
                newTexture = null;
            }
            
            oldValue = null;
        }
    }
}