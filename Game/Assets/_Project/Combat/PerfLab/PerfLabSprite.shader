// 성능 시제품용 스프라이트 셰이더 (PERF_PROTOTYPE.md 4장) + 옥토패스식 빛·그림자 (D-062 2단계)
// - 개체 데이터는 StructuredBuffer로 받고 SV_InstanceID로 읽는다 (DrawMeshInstancedProcedural)
// - 세로로 선 빌보드: 카메라 좌우 방향으로만 돌린다 (2.5D)
// - 시트는 열×행 격자. 프레임 번호 = 행 × 열 수 + 열 (왼쪽 위부터). 음수면 좌우 뒤집기 (-번호-1)
// - 도트 그림은 반투명 대신 알파 잘라내기 + 깊이 기록으로 그린다
// - 빛 (전역 _SpriteLit = 1일 때): 주변 빛 + 해·달 빛(감싸는 빛)을 받는다. 그림자 받기는 발 위치에서 한 번 재어
//   그림 전체가 같은 밝기(판끼리 반으로 갈리는 줄무늬 방지). 그림자 내기는 빛 쪽을 보는 판으로 그린다
// - 노멀맵(_NormalTex, _HasNormal = 1)이 있으면 그 방향으로 빛을 받는다 (D-062 G4 — 그림 확정 뒤 연결)
// 종류별 값(_MainTex, _Grid, _Pivot, _Aspect)은 MaterialPropertyBlock으로 넘긴다.
Shader "HLR/PerfLabSprite"
{
    Properties
    {
        _MainTex ("Sheet", 2D) = "white" {}
        _NormalTex ("Normal sheet (같은 칸)", 2D) = "bump" {}
        _HasNormal ("Has normal sheet", Float) = 0
        // 반사 마스크 (K-092·K-098, W3): R = 반사 강도, G = 매끄러움, 같은 칸. _HasSpec = 0이면 아무 계산·읽기도 안 한다 (균일 값 분기)
        _SpecTex ("Spec mask (R strength, G smoothness)", 2D) = "black" {}
        _HasSpec ("Has spec mask", Float) = 0
        _SpecGain ("Specular gain", Float) = 0.6
        _Grid("Grid (columns, rows)", Vector) = (4, 1, 0, 0)
        _Pivot ("Pivot (x, y in cell 0..1, from bottom-left)", Vector) = (0.5, 0, 0, 0)
        _Aspect ("Cell width / height", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        // 2번 지도 모드(BattleMap.md): 화면을 정면으로 보는 점으로 그린다. 0이면 기존 세로 빌보드 (PerfLab 그대로)
        _MapMode ("Map dot mode", Float) = 0
        _MapColor ("Map dot color (a=0: instance tint)", Color) = (0, 0, 0, 0)
        _MinWorldSize ("Map dot minimum size (world)", Float) = 0
        // 성벽 그림처럼 카메라를 따라 돌지 않고 정해진 방향으로 선 판 (w=1이면 xyz = 판의 오른쪽 방향)
        _FixedRight ("Fixed right axis (w=1 on)", Vector) = (1, 0, 0, 0)
        // 동작 그림 칸 구간 (K-032): 프레임 번호가 [x, y) 안인 개체만 그리고, 시트 칸 = 번호 − x. 기본은 전부
        _FrameWindow ("Frame window (start, end)", Vector) = (0, 100000, 0, 0)
        // 투사체 (K-032): 0 보통, 1 색을 _ShotColor로, 2 색을 _ShotColor로 + 개체 색 칸(rgb)을 날아가는 방향으로 보고 판을 돌린다
        _ShotMode ("Shot mode", Float) = 0
        _ShotColor ("Shot color", Color) = (1, 1, 1, 1)
        // 시트별 인물 크기 맞추기 (K-034): 개체 크기 × 이 값. 시트마다 칸 안 인물 키가 달라도 같은 크기로 (PerfLabArt.SheetScale)
        _SheetScale ("Sheet figure scale", Float) = 1
        // 위에서 내려볼 때 납작해 보이는 것 보정 (K-034): 1이면 판 높이를 1/cos(카메라에서 본 내려보는 각)만큼 늘린다.
        // 발(기준점)은 그대로라 발 위치·그림자 자리는 안 바뀐다. 그림자 판은 늘리지 않는다(실제 키 그대로)
        _ViewStretch ("Stretch against top-down squash (0/1)", Float) = 0
        // 성벽 바로 아래 적처럼 거의 수직으로 볼 때 지나치게 길어지지 않게
        _MaxViewStretch ("Max stretch", Float) = 1.4
        // 화면 기준으로 똑바로 선 판 (K-040, 옥토패스식): 1이면 판이 카메라 평면과 평행(오른쪽 = 카메라 오른쪽, 위 = 카메라 위).
        // 원근 카메라에서도 인물 세로선이 화면에 똑바로 선다. 이때 납작함 보정(_ViewStretch)은 쓰지 않는다(1).
        // 0이면 예전처럼 월드 위로 선 세로 판. _FixedRight 판·투사체·지도 점은 이 값과 상관없이 예전 방식. 그림자 판은 늘 세로 판
        _ScreenUpright ("Screen-aligned upright (0/1)", Float) = 1
        _GroundPlane ("Ground plane (0/1)", Float) = 0
        // 스스로 빛나는 그림 (K-041 불·폭발): 1이면 해·달 빛을 받지 않고 개체 색(1 넘는 밝은 색)을 그대로 — 빛번짐(Bloom)이 걸린다
        _Emissive ("Self-lit (0/1)", Float) = 0
        // 먼 적 저비용 그림 (W4b, K-099): (가장 가까운 거리, 가장 먼 거리, 1/4 칸 바꾸기, 켬). w = 0이면 거리 띠 없음(기본, 아무것도 안 함)
        _LodBand ("LOD band (min m, max m, quarter frames, on)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // C# PerfLabInstance와 같은 배치 (32바이트)
        struct PerfLabInstance
        {
            float4 posFrame;    // xyz 발 위치, w 프레임 번호
            float4 tintSize;    // rgb 색, a 크기(높이). 0이면 그리지 않음
        };

        StructuredBuffer<PerfLabInstance> _Instances;
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_NormalTex);
        SAMPLER(sampler_NormalTex);
        float _HasNormal;
        TEXTURE2D(_SpecTex);
        SAMPLER(sampler_SpecTex);
        float _HasSpec;
        float _SpecGain;
        float4 _Grid;
        float4 _Pivot;
        float _Aspect;
        float _Cutoff;
        float _MapMode;
        float4 _MapColor;
        float _MapSight;          // >0: 지도에서 성벽 바깥 이 거리(m)까지만 보인다 (적 점). 망원경 렌즈 안은 더 멀어도 보인다
        float _MapCastleHalf;
        float4 _MapLens;          // xyz 렌즈 가운데, w 반경 (0 = 렌즈 없음)
        float _MinWorldSize;
        float4 _FixedRight;
        float _SpriteLit;   // 전역: 1이면 빛을 받는다 (BattleLook)
        float4 _FrameWindow;
        float _ShotMode;
        float4 _ShotColor;
        float _SheetScale;
        float _ViewStretch;
        float _MaxViewStretch;
        float _ScreenUpright;
        float _GroundPlane;
        float _Emissive;
        float4 _LodBand;

        struct Attributes
        {
            float3 positionOS : POSITION;   // x -0.5..0.5, y 0..1
            float2 uv : TEXCOORD0;
            uint instanceID : SV_InstanceID;
        };

        // 프레임 번호가 음수면 좌우 뒤집기: 실제 번호 = -w - 1 (주인공 오른쪽 보기 등)
        float RawFrame(PerfLabInstance data)
        {
            return data.posFrame.w < 0.0 ? -data.posFrame.w - 1.0 : data.posFrame.w;
        }

        // 이 그리기(시트)의 칸 구간 안인가 (K-032)
        bool InWindow(PerfLabInstance data)
        {
            float frame = RawFrame(data);
            return frame > _FrameWindow.x - 0.5 && frame < _FrameWindow.y - 0.5;
        }

        // 먼 적 저비용 그림 거리 띠 (W4b): 카메라에서 이 개체까지 거리가 [min, max) 안인가. _LodBand.w = 0이면 항상 참
        bool InLodBand(PerfLabInstance data)
        {
            if (_LodBand.w < 0.5) return true;
            float3 toCamera = data.posFrame.xyz - _WorldSpaceCameraPos;
            float distanceSq = dot(toCamera, toCamera);
            return distanceSq >= _LodBand.x * _LodBand.x && distanceSq < _LodBand.y * _LodBand.y;
        }

        // 그리는 크기 = 개체 크기 × 시트 배율 (K-034)
        float DrawSize(PerfLabInstance data)
        {
            return data.tintSize.a * _SheetScale;
        }

        // 납작함 보정 배율 (K-034): 카메라에서 이 개체(몸 가운데)를 내려보는 각 φ — 세로 판은 cos φ만큼 짧아 보이므로 1/cos φ.
        // 개체마다 한 값(네 꼭짓점 같게). 보정을 끈 시트(_ViewStretch 0)는 1
        float ViewStretch(PerfLabInstance data, float size)
        {
            if (_ViewStretch < 0.5) return 1.0;
            float3 toCamera = _WorldSpaceCameraPos - (data.posFrame.xyz + float3(0.0, 0.5 * size, 0.0));
            float horizontal = length(toCamera.xz);
            float cosine = horizontal / max(length(toCamera), 1e-4);
            return min(1.0 / max(cosine, 1e-3), max(_MaxViewStretch, 1.0));
        }

        // 판 한 꼭짓점: 월드 위치, 시트 UV, 판의 오른쪽 방향(뒤집기 반영). stretch = 높이만 늘리는 배율 (발 기준).
        // upAxis = 판의 위 방향 (세로 판은 월드 위, 화면 기준 판은 카메라 위 — K-040)
        void Billboard(Attributes input, float3 right, float3 upAxis, float stretch, out float3 world, out float2 uv, out float3 faceRight)
        {
            PerfLabInstance data = _Instances[input.instanceID];
            float size = DrawSize(data);
            bool flip = data.posFrame.w < 0.0;
            float frame = max(RawFrame(data) - _FrameWindow.x, 0.0);
            if (_LodBand.z > 0.5) frame = fmod(frame, 4.0) >= 2.0 ? 1.0 : 0.0;   // 1/4 시트: 걷기 4칸 → 두 자세 (FarSprites.QuarterFrame)
            float pivotX = flip ? 1.0 - _Pivot.x : _Pivot.x;
            float localX = (input.positionOS.x + 0.5 - pivotX) * _Aspect * size;
            float localY = (input.positionOS.y - _Pivot.y) * size * stretch;
            if (_GroundPlane > 0.5)
            {
                // 폭발 뒤 남는 구덩이는 서 있는 판이 아니라 실제 X/Z 바닥에 놓는다.
                world = data.posFrame.xyz + float3(localX, 0.0, (input.positionOS.y - _Pivot.y) * size);
            }
            else if (_ShotMode > 1.5)
            {
                // 투사체 (K-040): 축 고정 빌보드 — 판의 긴 축 = 3D로 날아가는 방향(개체 색 칸, 그림 +X = 촉),
                // 판은 그 축 둘레로만 돌아 넓은 면이 카메라 쪽. 옆에서 보면 길게, 뒤에서 보면 짧게(촉이 저쪽·깃이 이쪽) 보인다.
                // 날아가며 내려가면 촉도 내려간다. 납작함 보정(stretch)은 쓰지 않는다
                float3 axis = data.tintSize.rgb;
                axis = dot(axis, axis) > 1e-6 ? normalize(axis) : right;
                float3 side = cross(axis, _WorldSpaceCameraPos - data.posFrame.xyz);
                if (dot(side, side) < 1e-8) side = cross(axis, abs(axis.y) < 0.9 ? float3(0.0, 1.0, 0.0) : right);   // 축이 카메라를 곧장 볼 때
                side = normalize(side);
                float alongX = (input.positionOS.x + 0.5 - _Pivot.x) * _Aspect * size;
                float acrossY = (input.positionOS.y - _Pivot.y) * size;
                world = data.posFrame.xyz + axis * alongX + side * acrossY;
            }
            else
            {
                world = data.posFrame.xyz + right * localX + upAxis * localY;
            }

            float columns = max(_Grid.x, 1.0);
            float rows = max(_Grid.y, 1.0);
            float column = fmod(frame, columns);
            float row = floor(frame / columns);
            float u = flip ? 1.0 - input.uv.x : input.uv.x;
            uv = float2((column + u) / columns, 1.0 - (row + 1.0 - input.uv.y) / rows);
            faceRight = flip ? -right : right;
        }

        float3 CameraRight()
        {
            // 뷰 행렬 첫 행 = 카메라 오른쪽 방향(월드). 높이 성분을 빼서 세로로 선 빌보드
            float3 camRight = float3(UNITY_MATRIX_V[0].x, 0.0, UNITY_MATRIX_V[0].z);
            float lenSq = dot(camRight, camRight);
            camRight = lenSq > 1e-6 ? camRight * rsqrt(lenSq) : float3(1.0, 0.0, 0.0);
            if (_FixedRight.w > 0.5) camRight = _FixedRight.xyz;
            return camRight;
        }
        ENDHLSL

        Pass
        {
            Name "PerfLabSprite"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // 추가 조명 (횃불·폭발 빛, D-062 4단계). 포워드 플러스는 6.1부터 _CLUSTER_LIGHT_LOOP (예전 _FORWARD_PLUS)
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            // 안개 (K-041): 예전에는 그림이 안개를 받지 않아 안개 날에도 적이 또렷했다
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 tint : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;   // 발 위치 기준 (그림 전체 같은 값)
                float3 faceRight : TEXCOORD3;
                float3 faceNormal : TEXCOORD4;    // 카메라 쪽 (수평)
                float lit : TEXCOORD5;            // 0 = 빛 계산 안 함 (지도 점)
                float3 positionWS : TEXCOORD6;    // 추가 조명 거리 감쇠용 (픽셀마다)
                float fogFactor : TEXCOORD7;      // 안개 (지도 점은 안 받는다 — fogOn 0)
                float fogOn : TEXCOORD8;
            };

            // 추가 조명 하나: 해·달과 같은 감싸는 빛 × 거리 감쇠. 그림자는 받지 않는다 (주 조명만)
            half3 WrapAdditional(Light light, float3 n)
            {
                float wrap = saturate((dot(n, light.direction) + 0.6) / 1.6);
                return light.color * (wrap * light.distanceAttenuation * 0.85);
            }

            // 반사광 한 줄기 (Blinn-Phong, W3): 빛 색(해·달은 밝기·그림자 포함, 횃불은 거리 감쇠 포함) × (n·h)^power × 앞면 감쇠
            half3 SpecTerm(half3 lightColor, float3 lightDir, float3 n, float3 viewDir, float power)
            {
                float nh = saturate(dot(n, normalize(lightDir + viewDir)));
                float nl = saturate(dot(n, lightDir) * 2.0);   // 빛이 등 뒤에서 오면 번쩍이지 않게
                return lightColor * (pow(nh, power) * nl);
            }

            Varyings Vert(Attributes input)
            {
                PerfLabInstance data = _Instances[input.instanceID];
                float size = data.tintSize.a;
                Varyings output = (Varyings)0;
                if (!InWindow(data) || !InLodBand(data))
                {
                    output.positionCS = float4(0.0, 0.0, -2.0, 1.0);   // 다른 시트 구간 칸 또는 이 그림의 거리 띠 밖 (잘려 나감)
                    return output;
                }

                if (_MapMode > 0.5)
                {
                    // 지도: 카메라를 정면으로 보는 점(가운데 기준), 너무 작으면 최소 크기. 크기 0(숨김)은 그대로 숨김
                    float dot = size > 0.0 ? max(size, _MinWorldSize) : 0.0;
                    if (_MapSight > 0.0)
                    {
                        // 지도 시야 (사용자 2026-10-20 '지금은 어디서 오는지 다 보인다'): 성벽에서 시야 밖이고 망원경 렌즈 밖이면 숨김
                        float fromWall = max(abs(data.posFrame.x), abs(data.posFrame.z)) - _MapCastleHalf;
                        float2 toLens = data.posFrame.xz - _MapLens.xz;
                        bool inLens = _MapLens.w > 0.0 && (toLens.x * toLens.x + toLens.y * toLens.y) < _MapLens.w * _MapLens.w;   // (지역 변수 dot이 dot()을 가린다)
                        if (fromWall > _MapSight && !inLens) dot = 0.0;
                    }
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    float3 mapWorld = data.posFrame.xyz + right * (input.positionOS.x * dot) + up * ((input.positionOS.y - 0.5) * dot);
                    output.positionCS = TransformWorldToHClip(mapWorld);
                    output.uv = input.uv;
                    output.tint = _MapColor.a > 0.0 ? (half3)_MapColor.rgb : (half3)data.tintSize.rgb;
                    output.lit = 0.0;
                    return output;
                }

                float3 camRight = CameraRight();
                float3 world; float2 uv; float3 faceRight;
                size = DrawSize(data);
                // 화면 기준으로 똑바로 (K-040): 카메라 평면과 평행한 판, 발 기준점은 그대로. 납작함 보정은 필요 없다
                bool upright = _ScreenUpright > 0.5 && _FixedRight.w < 0.5 && _ShotMode < 0.5;
                float3 upAxis = float3(0.0, 1.0, 0.0);
                float stretch = ViewStretch(data, size);
                // 빛 계산 표면 방향: 예전 세로 판의 cross(오른쪽, 위)와 같은 부호(= -V[2])를 지켜 밝기 느낌이 뒤집히지 않게
                float3 faceNormal = normalize(cross(camRight, float3(0.0, 1.0, 0.0)));
                if (upright)
                {
                    camRight = normalize(UNITY_MATRIX_V[0].xyz);
                    upAxis = normalize(UNITY_MATRIX_V[1].xyz);
                    stretch = 1.0;
                    faceNormal = -normalize(UNITY_MATRIX_V[2].xyz);
                }

                Billboard(input, camRight, upAxis, stretch, world, uv, faceRight);
                if (_GroundPlane > 0.5) faceNormal = float3(0.0, 1.0, 0.0);
                output.positionCS = TransformWorldToHClip(world);
                output.positionWS = world;
                output.uv = uv;
                output.tint = _ShotMode > 0.5 ? (half3)_ShotColor.rgb : (half3)data.tintSize.rgb;
                output.faceRight = faceRight;
                output.faceNormal = faceNormal;
                output.shadowCoord = TransformWorldToShadowCoord(data.posFrame.xyz + float3(0.0, 0.25 * size, 0.0));
                output.lit = _Emissive > 0.5 ? 0.0 : _SpriteLit;   // 스스로 빛나는 그림은 빛 계산 없이 색 그대로 (K-041)
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.fogOn = 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(color.a - _Cutoff);
                half3 albedo = color.rgb * input.tint;
                if (input.lit < 0.5) return half4(input.fogOn > 0.5 ? MixFog(albedo, input.fogFactor) : albedo, 1.0h);

                // 표면 방향: 노멀맵이 있으면 그 방향, 없으면 카메라 쪽에서 조금 위로 (감싸는 빛)
                float3 up = float3(0.0, 1.0, 0.0);
                float3 n = normalize(input.faceNormal + up * 0.35);
                if (_HasNormal > 0.5)
                {
                    half3 tangentNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, input.uv));
                    n = normalize(input.faceRight * tangentNormal.x + up * tangentNormal.y + input.faceNormal * tangentNormal.z);
                }

                Light mainLight = GetMainLight(input.shadowCoord);
                float wrap = saturate((dot(n, mainLight.direction) + 0.6) / 1.6);
                half3 sun = mainLight.color * (wrap * mainLight.shadowAttenuation * 0.85);
                half3 ambient = SampleSH(n) * 0.75;

                // 반사 (W3): 마스크가 없으면(_HasSpec 0, 균일 값) 이 블록·아래 반사 항 모두 건너뛴다
                bool specOn = _HasSpec > 0.5;
                half3 spec = 0;
                float specStrength = 0.0;
                float specPower = 1.0;
                float3 viewDir = float3(0.0, 0.0, 1.0);
                [branch] if (specOn)
                {
                    half2 mask = SAMPLE_TEXTURE2D_LOD(_SpecTex, sampler_SpecTex, input.uv, 0).rg;   // 분기 안이라 미분 없는 표본 (가면은 밉맵 없음)
                    specStrength = mask.r * _SpecGain;
                    specPower = exp2(8.0 * mask.g + 1.0);   // 매끄러움 0 → 2(넓은 번짐), 1 → 512(좁은 점)
                    viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                    spec += SpecTerm(mainLight.color * mainLight.shadowAttenuation, mainLight.direction, n, viewDir, specPower);
                }

                // 추가 조명 (횃불·폭발): 포워드 플러스면 화면 칸 목록, 아니면 개체별 목록을 돈다
                half3 extra = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;   // LIGHT_LOOP_BEGIN(포워드 플러스)이 이 이름으로 읽는다
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint lightsCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                        // 포워드 플러스: 추가 방향 빛은 칸 목록 밖에 따로 있다
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light dirLight = GetAdditionalLight(lightIndex, input.positionWS);
                            extra += WrapAdditional(dirLight, n);
                            [branch] if (specOn) spec += SpecTerm(dirLight.color * dirLight.distanceAttenuation, dirLight.direction, n, viewDir, specPower);
                        }
                    #endif
                    LIGHT_LOOP_BEGIN(lightsCount)
                        Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                        extra += WrapAdditional(addLight, n);
                        [branch] if (specOn) spec += SpecTerm(addLight.color * addLight.distanceAttenuation, addLight.direction, n, viewDir, specPower);
                    LIGHT_LOOP_END
                #endif

                // 반사 색: 흰빛에 그림 색을 반쯤 섞는다 (금·구리는 금빛, 철은 흰빛). 해·달 색(밤 푸른빛·NightTuning.SunScale)과 횃불 색이 이미 spec에 들어 있다
                half3 specColor = spec * specStrength * lerp(half3(1, 1, 1), saturate(albedo * 2.0h), 0.5h);
                return half4(MixFog(albedo * (sun + ambient + extra) + specColor, input.fogFactor), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 4.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;   // 그림자 그리는 빛 방향 (유니티가 넣어 준다)

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                PerfLabInstance data = _Instances[input.instanceID];
                if (_MapMode > 0.5 || data.tintSize.a <= 0.0 || _SpriteLit < 0.5 || !InWindow(data))
                {
                    output.positionCS = float4(0.0, 0.0, -2.0, 1.0);   // 그림자 없음 (잘려 나감)
                    return output;
                }

                // 빛 쪽을 보는 판: 오른쪽 = 수평으로 놓은 빛 방향에 수직
                float3 light = _LightDirection;
                float3 flat = float3(light.x, 0.0, light.z);
                float3 right = dot(flat, flat) > 1e-4 ? normalize(cross(float3(0.0, 1.0, 0.0), flat)) : CameraRight();
                if (_FixedRight.w > 0.5) right = _FixedRight.xyz;
                float3 world; float2 uv; float3 faceRight;
                Billboard(input, right, float3(0.0, 1.0, 0.0), 1.0, world, uv, faceRight);   // 그림자는 늘 세로 판·실제 키 그대로 (납작함 보정 없음)
                float3 normal = normalize(cross(right, float3(0.0, 1.0, 0.0)));
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(world, normal, light));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = uv;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
