using Lime;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProtoBuf;
using System.ComponentModel;

namespace Lime
{
	[ProtoContract]
	public class ParticleModifier : Node
	{
		[ProtoMember(1)]
		[DefaultValue(1f)]
		public float Scale { get; set; }

		[ProtoMember(2)]
		[DefaultValue(1f)]
		public float AspectRatio { get; set; }

		[ProtoMember(3)]
		[DefaultValue(1f)]
		public float Velocity { get; set; }

		[ProtoMember(4)]
		[DefaultValue(1f)]
		public float Spin { get; set; }

		[ProtoMember(5)]
		[DefaultValue(1f)]
		public float AngularVelocity { get; set; }

		[ProtoMember(6)]
		[DefaultValue(1f)]
		public float GravityAmount { get; set; }

		[ProtoMember(7)]
		[DefaultValue(1f)]
		public float WindAmount { get; set; }

		[ProtoMember(8)]
		[DefaultValue(1f)]
		public float MagnetAmount { get; set; }

		[ProtoMember(9)]
		public Color4 Color { get; set; }

		[ProtoMember(10)]
		[DefaultValue(1)]
		public int FirstFrame { get; set; }

		[ProtoMember(11)]
		[DefaultValue(1)]
		public int LastFrame { get; set; }

		[ProtoMember(12)]
		public float AnimationFps { get; set; }

		[ProtoMember(13)]
		[DefaultValue(true)]
		public bool LoopedAnimation { get; set; }

		SerializableTexture texture = new SerializableTexture();
		[ProtoMember(14)]
		public SerializableTexture Texture { get { return texture; } set { texture = value; textures = null; } }

		public ParticleModifier()
		{
			Scale = 1;
			AspectRatio = 1;
			Velocity = 1;
			Spin = 1;
			AngularVelocity = 1;
			WindAmount = 1;
			GravityAmount = 1;
			MagnetAmount = 1;
			Color = Color4.White;
			FirstFrame = 1;
			LastFrame = 1;
			AnimationFps = 20;
			LoopedAnimation = true;
		}

		bool ChangeTextureFrameIndex(ref string path, int frame)
		{
			if (frame < 0 || frame > 99)
				return false;
			int i = path.Length - 1;
			for (; i >= 0; i--)
				if (path[i] == '.')
					break;
			if (i < 2)
				return false;
			if (char.IsDigit(path, i - 1) && char.IsDigit(path, i - 2)) {
				var s = new StringBuilder(path);
				s[i - 1] = (char)(frame % 10 + '0');
				s[i - 2] = (char)(frame / 10 + '0');
				path = s.ToString();
				return true;
			}
			return false;
		}

		List<SerializableTexture> textures;
		internal SerializableTexture GetTexture(int index)
		{
			if (textures == null) {
				textures = new List<SerializableTexture>();
				var path = texture.Path;
				for (int i = 0; i < 100; i++) {
					if (!ChangeTextureFrameIndex(ref path, i))
						break;
					if (File.Exists(path)) {
						var t = new SerializableTexture(path);
						textures.Add(t);
					} else if (i > 0)
						break;
				}
			}
			if (textures.Count == 0)
				return texture;
			index = MathLib.Clamp(index, 0, textures.Count - 1);
			return textures[index];
		}
	}
}
