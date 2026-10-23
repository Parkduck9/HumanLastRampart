using System.Collections.Generic;
using UnityEngine;

namespace HumanLastRampart.Combat.Battle
{
    // 그림 판(아틀라스) 칸 좌표로 면마다 UV를 주는 작은 메시 쌓기 (W27, K-116 상자·통·계단·데칼·탑 안 소품이 같이 쓴다)
    // - 면 하나 = 정점 4개, 그림 칸(Rect, 0~1, 왼쪽 아래 원점)을 그대로 입힌다 — 늘어나지 않게 면 크기에 맞는 칸을 고른다
    // - 사각형 순서: 바깥에서 볼 때 시계 방향 (왼아래 → 왼위 → 오른위 → 오른아래) — 유니티 앞면 규칙
    // - 한 메시 = 그리기 1번. 만드는 시점에만 쓰고 프레임마다 쓰지 않는다
    internal sealed class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public int VertexCount => verts.Count;

        // 위쪽 왼쪽 원점 화소 좌표 → UV 칸 (칸 가장자리에서 아주 조금 안쪽)
        public static Rect Px(int x, int y, int w, int h, int atlasW, int atlasH)
        {
            const float inset = 0.02f;
            return new Rect((x + inset) / atlasW, (atlasH - (y + h) + inset) / atlasH, (w - 2f * inset) / atlasW, (h - 2f * inset) / atlasH);
        }

        public void Face(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 normal, Rect uv)
            => Face(bl, tl, tr, br, normal, normal, normal, normal, uv);

        public void Face(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 nbl, Vector3 ntl, Vector3 ntr, Vector3 nbr, Rect uv)
        {
            int s = verts.Count;
            verts.Add(bl); verts.Add(tl); verts.Add(tr); verts.Add(br);
            normals.Add(nbl); normals.Add(ntl); normals.Add(ntr); normals.Add(nbr);
            uvs.Add(new Vector2(uv.xMin, uv.yMin)); uvs.Add(new Vector2(uv.xMin, uv.yMax)); uvs.Add(new Vector2(uv.xMax, uv.yMax)); uvs.Add(new Vector2(uv.xMax, uv.yMin));
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }

        // 바닥 중심·크기(가로·높이·세로)·좌우 회전(도). 옆 넷과 윗면 (아랫면은 바닥에 닿아 없다)
        // 앞 = +z, 오른쪽 = +x, 뒤 = -z, 왼쪽 = -x (회전 전 기준)
        public void Box(Vector3 baseCenter, Vector3 size, float yawDegrees, Rect front, Rect right, Rect back, Rect left, Rect top)
        {
            int start = verts.Count;
            Vector3 lo = new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f), hi = new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
            Face(new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(lo.x, lo.y, hi.z), Vector3.forward, front);
            Face(new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, lo.y, hi.z), Vector3.right, right);
            Face(new Vector3(lo.x, lo.y, lo.z), new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, lo.y, lo.z), Vector3.back, back);
            Face(new Vector3(lo.x, lo.y, hi.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(lo.x, hi.y, lo.z), new Vector3(lo.x, lo.y, lo.z), Vector3.left, left);
            Face(new Vector3(lo.x, hi.y, lo.z), new Vector3(lo.x, hi.y, hi.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(hi.x, hi.y, lo.z), Vector3.up, top);
            Place(start, baseCenter, yawDegrees);
        }

        // 한 칸 그림을 옆 넷·윗면에 모두 (색 한 가지 소품)
        public void Box(Vector3 baseCenter, Vector3 size, float yawDegrees, Rect all)
            => Box(baseCenter, size, yawDegrees, all, all, all, all, all);

        // 가운데가 볼록한 통 (아래·위 반지름 0.9배). side = 옆면 둘레 한 바퀴 띠, top = 윗면 원판 (원판 그림은 칸 안에 내접)
        public void Barrel(Vector3 baseCenter, float radius, float height, int segments, Rect side, Rect top)
        {
            float[] ring = { 0.9f, 1f, 0.9f };
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                float u0 = Mathf.Lerp(side.xMin, side.xMax, (float)i / segments), u1 = Mathf.Lerp(side.xMin, side.xMax, (float)(i + 1) / segments);
                for (int row = 0; row < 2; row++)
                {
                    float y0 = height * row * 0.5f, y1 = height * (row + 1) * 0.5f;
                    float v0 = Mathf.Lerp(side.yMin, side.yMax, row * 0.5f), v1 = Mathf.Lerp(side.yMin, side.yMax, (row + 1) * 0.5f);
                    Face(baseCenter + d0 * radius * ring[row] + Vector3.up * y0, baseCenter + d0 * radius * ring[row + 1] + Vector3.up * y1,
                        baseCenter + d1 * radius * ring[row + 1] + Vector3.up * y1, baseCenter + d1 * radius * ring[row] + Vector3.up * y0,
                        d0, d0, d1, d1, Rect.MinMaxRect(u0, v0, u1, v1));
                }
            }

            // 윗면: 가운데 한 점 부채꼴
            int c = verts.Count;
            Vector2 uc = top.center;
            float ur = Mathf.Min(top.width, top.height) * 0.5f * 0.96f;
            verts.Add(baseCenter + Vector3.up * height); normals.Add(Vector3.up); uvs.Add(uc);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts.Add(baseCenter + Vector3.up * height + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.9f);
                normals.Add(Vector3.up);
                uvs.Add(uc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ur);
            }

            for (int i = 0; i < segments; i++) { tris.Add(c); tris.Add(c + 2 + i); tris.Add(c + 1 + i); }
        }

        // 바닥에 깔리는 납작한 사각형 (데칼). 가운데·가로(x)·세로(z)·90도 단위 회전
        public void Flat(Vector3 center, float width, float depth, int quarterTurns, Rect uv)
        {
            int start = verts.Count;
            float hx = width * 0.5f, hz = depth * 0.5f;
            Face(new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz), Vector3.up, uv);
            Place(start, center, quarterTurns * 90f);
        }

        // 위·아래가 같은 폭의 세운 판 (사진기를 보는 판의 바탕 모양). 아래 가운데가 원점, 보는 쪽 = -z
        public void Standing(float width, float height, Rect uv)
        {
            float hx = width * 0.5f;
            Face(new Vector3(-hx, 0f, 0f), new Vector3(-hx, height, 0f), new Vector3(hx, height, 0f), new Vector3(hx, 0f, 0f), Vector3.back, uv);
        }

        // 정점 [start, 끝)을 좌우 회전한 뒤 옮긴다
        void Place(int start, Vector3 offset, float yawDegrees)
        {
            Quaternion q = Quaternion.Euler(0f, yawDegrees, 0f);
            for (int i = start; i < verts.Count; i++)
            {
                verts[i] = q * verts[i] + offset;
                normals[i] = q * normals[i];
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
