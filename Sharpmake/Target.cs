﻿// Copyright (c) 2017 Ubisoft Entertainment
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Sharpmake
{
    // Mandatory
    [Fragment, Flags]
    public enum DevEnv
    {
        vs2010 = 1 << 0,
        vs2012 = 1 << 1,
        vs2013 = 1 << 2,
        vs2015 = 1 << 3,
        vs2017 = 1 << 4,
        xcode4ios = 1 << 5,
        eclipse = 1 << 6,
        make = 1 << 7
    }

    // Mandatory
    [Fragment, Flags]
    public enum Platform
    {
        win32 = 1 << 0,
        win64 = 1 << 1,
        x360 = 1 << 4,
        ps3 = 1 << 5,
        ps3spu = 1 << 6,
        durango = 1 << 7,
        orbis = 1 << 8,
        wiiu = 1 << 9,
        anycpu = 1 << 10,
        wii = 1 << 11,
        ctr = 1 << 12,
        ios = 1 << 13,
        android = 1 << 14,
        nx = 1 << 15,
        nvshield = 1 << 16,
        linux = 1 << 17,
        mac = 1 << 18
    }

    [Fragment, Flags]
    public enum BuildSystem
    {
        MSBuild = 0x01,
        FastBuild = 0x02,
    }

    [Fragment, Flags]
    public enum Optimization
    {
        Debug = 0x01,
        Release = 0x02,
        Retail = 0x04
    }

    [Fragment, Flags]
    public enum OutputType
    {
        Lib = 0x01,
        Dll = 0x02,
    }

    [Fragment, Flags]
    public enum DotNetFramework
    {
        v2 = 1 << 0,
        v3 = 1 << 1,
        v3_5 = 1 << 2,
        v3_5clientprofile = 1 << 3,
        v4_0 = 1 << 4,
        v4_5 = 1 << 5,
        v4_5_1 = 1 << 6,
        v4_5_2 = 1 << 7,
        v4_5clientprofile = 1 << 8,
        v4_6 = 1 << 9,
        v4_6_1 = 1 << 10,
        v4_6_2 = 1 << 11,
    }

    // Optional
    [Fragment, Flags]
    public enum Blob
    {
        // Blob only project, another project reference the source files
        Blob = 0x01,

        // Normal Visual Studio project without blobbing.
        // Can be combined with Blob inside same solution.
        NoBlob = 0x02,

        FastBuildUnitys = 0x04,
    }

    public enum KitsRootEnum
    {
        KitsRoot,
        KitsRoot81,
        KitsRoot10
    }

    public class KitsRootPaths
    {
        private static Dictionary<DevEnv, KitsRootEnum> s_defaultKitsRootForDevEnv = new Dictionary<DevEnv, KitsRootEnum>();
        private static Dictionary<KitsRootEnum, string> s_defaultKitsRoots = new Dictionary<KitsRootEnum, string>();

        private static Dictionary<DevEnv, KitsRootEnum> s_useKitsRootForDevEnv = new Dictionary<DevEnv, KitsRootEnum>();
        private static Dictionary<KitsRootEnum, string> s_kitsRoots = new Dictionary<KitsRootEnum, string>();

        private static Dictionary<DotNetFramework, string> s_netFxKitsDir = new Dictionary<DotNetFramework, string>();

        public static Options.Vc.General.WindowsTargetPlatformVersion WindowsTargetPlatformVersion { get; private set; } = Options.Vc.General.WindowsTargetPlatformVersion.v8_1;

        private static KitsRootPaths s_kitsRootsInstance = new KitsRootPaths();

        public KitsRootPaths()
        {
            string kitsRegistryKeyString = string.Format(@"SOFTWARE{0}\Microsoft\Windows Kits\Installed Roots",
                Environment.Is64BitProcess ? @"\Wow6432Node" : string.Empty);

            s_defaultKitsRoots[KitsRootEnum.KitsRoot] = Util.GetRegistryLocalMachineSubKeyValue(kitsRegistryKeyString, KitsRootEnum.KitsRoot.ToString(), @"C:\Program Files (x86)\Windows Kits\8.0\");
            s_defaultKitsRoots[KitsRootEnum.KitsRoot81] = Util.GetRegistryLocalMachineSubKeyValue(kitsRegistryKeyString, KitsRootEnum.KitsRoot81.ToString(), @"C:\Program Files (x86)\Windows Kits\8.1\");
            s_defaultKitsRoots[KitsRootEnum.KitsRoot10] = Util.GetRegistryLocalMachineSubKeyValue(kitsRegistryKeyString, KitsRootEnum.KitsRoot10.ToString(), @"C:\Program Files (x86)\Windows Kits\10\");

            string netFXSdkRegistryKeyString = string.Format(@"SOFTWARE{0}\Microsoft\Microsoft SDKs\NETFXSDK",
                Environment.Is64BitProcess ? @"\Wow6432Node" : string.Empty);
            s_netFxKitsDir[DotNetFramework.v4_6] = Util.GetRegistryLocalMachineSubKeyValue(netFXSdkRegistryKeyString + @"\" + DotNetFramework.v4_6.ToVersionString(), "KitsInstallationFolder", @"C:\Program Files (x86)\Windows Kits\NETFXSDK\4.6\");
            s_netFxKitsDir[DotNetFramework.v4_6_1] = Util.GetRegistryLocalMachineSubKeyValue(netFXSdkRegistryKeyString + @"\" + DotNetFramework.v4_6_1.ToVersionString(), "KitsInstallationFolder", @"C:\Program Files (x86)\Windows Kits\NETFXSDK\4.6.1\");

            s_defaultKitsRootForDevEnv[DevEnv.vs2012] = KitsRootEnum.KitsRoot;
            s_defaultKitsRootForDevEnv[DevEnv.vs2013] = KitsRootEnum.KitsRoot81;
            s_defaultKitsRootForDevEnv[DevEnv.vs2015] = KitsRootEnum.KitsRoot81;
            s_defaultKitsRootForDevEnv[DevEnv.vs2017] = KitsRootEnum.KitsRoot10;
        }

        public static string GetRoot(KitsRootEnum kitsRoot)
        {
            if (s_kitsRootsInstance == null)
                throw new Error();

            if (s_kitsRoots.ContainsKey(kitsRoot))
                return s_kitsRoots[kitsRoot];

            if (s_defaultKitsRoots.ContainsKey(kitsRoot))
                return s_defaultKitsRoots[kitsRoot];

            throw new NotImplementedException("No Root associated with " + kitsRoot.ToString());
        }

        public static string GetDefaultRoot(KitsRootEnum kitsRoot)
        {
            if (s_kitsRootsInstance == null)
                throw new Error();

            if (s_defaultKitsRoots.ContainsKey(kitsRoot))
                return s_defaultKitsRoots[kitsRoot];

            throw new NotImplementedException("No DefaultKitsRoots associated with " + kitsRoot.ToString());
        }

        public static void SetRoot(KitsRootEnum kitsRoot, string kitsRootPath)
        {
            s_kitsRoots[kitsRoot] = kitsRootPath;
        }

        public static KitsRootEnum GetUseKitsRootForDevEnv(DevEnv devEnv)
        {
            if (s_useKitsRootForDevEnv.ContainsKey(devEnv))
                return s_useKitsRootForDevEnv[devEnv];

            if (s_defaultKitsRootForDevEnv.ContainsKey(devEnv))
                return s_defaultKitsRootForDevEnv[devEnv];

            throw new NotImplementedException("No UseKitsRoot associated with " + devEnv.ToString());
        }

        public static bool IsDefaultKitRootPath(DevEnv devEnv)
        {
            KitsRootEnum kitsRoot = GetUseKitsRootForDevEnv(devEnv);
            return GetDefaultRoot(kitsRoot) == GetRoot(kitsRoot);
        }

        public static void SetUseKitsRootForDevEnv(DevEnv devEnv, KitsRootEnum kitsRoot, Options.Vc.General.WindowsTargetPlatformVersion? windowsTargetPlatformVersion = null)
        {
            s_useKitsRootForDevEnv[devEnv] = kitsRoot;
            switch (kitsRoot)
            {
                case KitsRootEnum.KitsRoot:
                    if (windowsTargetPlatformVersion.HasValue)
                        throw new Error("Unsupported setting: WindowsTargetPlatformVersion is not customizable for KitsRoot 8.0.");
                    break;
                case KitsRootEnum.KitsRoot81:
                    if (windowsTargetPlatformVersion.HasValue && windowsTargetPlatformVersion.Value != Options.Vc.General.WindowsTargetPlatformVersion.v8_1)
                        throw new Error("Unsupported setting: WindowsTargetPlatformVersion is not customizable for KitsRoot 8.1. Redundant setting will be discarded");
                    break;
                case KitsRootEnum.KitsRoot10:
                    if (!windowsTargetPlatformVersion.HasValue)
                        windowsTargetPlatformVersion = Options.Vc.General.WindowsTargetPlatformVersion.v10_0_10586_0;

                    if (windowsTargetPlatformVersion.Value == Options.Vc.General.WindowsTargetPlatformVersion.v8_1)
                        throw new Error("Inconsistent values detected: KitsRoot10 set for " + devEnv + ", but windowsTargetPlatform is set to 8.1");

                    WindowsTargetPlatformVersion = windowsTargetPlatformVersion.Value;
                    break;
            }
        }

        public static string GetWindowsTargetPlatformVersion()
        {
            switch (WindowsTargetPlatformVersion)
            {
                case Options.Vc.General.WindowsTargetPlatformVersion.v8_1: return "8.1";
                case Options.Vc.General.WindowsTargetPlatformVersion.v10_0_10240_0: return "10.0.10240.0";
                case Options.Vc.General.WindowsTargetPlatformVersion.v10_0_10586_0: return "10.0.10586.0";
                case Options.Vc.General.WindowsTargetPlatformVersion.v10_0_14393_0: return "10.0.14393.0";
                case Options.Vc.General.WindowsTargetPlatformVersion.v10_0_15063_0: return "10.0.15063.0";
                default:
                    throw new ArgumentOutOfRangeException("WindowsTargetPlatformVersion");
            }
        }

        public static string GetNETFXKitsDir(DotNetFramework dotNetFramework)
        {
            if (s_netFxKitsDir.ContainsKey(dotNetFramework))
                return s_netFxKitsDir[dotNetFramework];

            throw new NotImplementedException("No NETFXKitsDir associated with " + dotNetFramework.ToString());
        }
    }

    // Default Target, user may define its own if needed
    public class Target : ITarget
    {
        public Optimization Optimization;
        public Platform Platform;
        public BuildSystem BuildSystem;
        public DevEnv DevEnv;
        public OutputType OutputType;
        public DotNetFramework Framework;
        public Blob Blob;

        public override string Name
        {
            get { return Optimization.ToString(); }
        }

        public Target() { }

        public Target(
            Platform platform,
            DevEnv devEnv,
            Optimization optimization,
            OutputType outputType = OutputType.Lib,
            Blob blob = Blob.NoBlob,
            BuildSystem buildSystem = BuildSystem.MSBuild,
            DotNetFramework framework = DotNetFramework.v3_5
        )
        {
            Platform = platform;
            DevEnv = devEnv;
            Optimization = optimization;
            OutputType = outputType;
            Framework = framework;
            BuildSystem = buildSystem;
            Blob = blob;
        }
    }

    public abstract class ITarget : IComparable<ITarget>
    {
        public override string ToString()
        {
            return GetTargetString();
        }

        private static ConcurrentDictionary<object, string> s_cachedFieldValueToString = new ConcurrentDictionary<object, string>();

        public string GetTargetString()
        {
            FieldInfo[] fieldInfos = GetFragmentFieldInfo();
            string result = String.Join("_", fieldInfos.Select(f => s_cachedFieldValueToString.GetOrAdd(f.GetValue(this), value => value.ToString())).ToArray());
            return result;
        }

        public virtual string Name
        {
            get { return GetTargetString(); }
        }

        public virtual string ProjectConfigurationName
        {
            get { return Name; }
        }

        public ITarget Clone(params object[] overrideValues)
        {
            Type sourceType = GetType();
            ITarget destination = Activator.CreateInstance(sourceType) as ITarget;

            FieldInfo[] fragmentFields = GetFragmentFieldInfo();

            foreach (FieldInfo fragmentField in fragmentFields)
            {
                int sourceFragmentValue = (int)fragmentField.GetValue(this);
                fragmentField.SetValue(destination, sourceFragmentValue);
            }

            if (overrideValues.Length > 0)
            {
                destination.SetFragments(overrideValues);
            }

            return destination;
        }

        public static void ValidFragmentType(Type fragmentType)
        {
            if (!fragmentType.IsEnum)
                throw new Error("fragment must be an Enum: {0}", fragmentType.FullName);

            if (!fragmentType.IsDefined(typeof(Fragment), false))
                throw new Error("fragment must have [Sharpmake.Fragment] attribute: {0}", fragmentType.FullName);

            if (!fragmentType.IsDefined(typeof(FlagsAttribute), false))
                throw new Error("fragment must have [Flags] attribute: {0}", fragmentType.FullName);
        }

        public int CompareTo(ITarget other)
        {
            if (GetType() != other.GetType())
            {
                int cmp = GetType().FullName.CompareTo(other.GetType().FullName);
                if (cmp == 0)
                    throw new Exception("Two different types cannot have same name: " + GetType().FullName);
                return cmp;
            }

            if (_valueCache == null)
                _valueCache = GetTargetString();

            if (other._valueCache == null)
                other._valueCache = other.GetTargetString();

            return _valueCache.CompareTo(other._valueCache);
        }


        public bool IsEqualTo(ITarget other)
        {
            if (GetType() != other.GetType())
                return false;

            if (_valueCache == null)
                _valueCache = GetTargetString();

            if (other._valueCache == null)
                other._valueCache = other.GetTargetString();

            return _valueCache == other._valueCache;
        }

        //possible to override this to make the associations with custom platforms and Sharpmake's
        public virtual Platform GetPlatform()
        {
            return GetFragment<Platform>();
        }

        //possible to override this to make the associations with custom platforms and Sharpmake's
        public virtual Optimization GetOptimization()
        {
            return GetFragment<Optimization>();
        }

        public T GetFragment<T>()
        {
            FieldInfo[] fragments = GetType().GetFields();
            foreach (FieldInfo fragment in fragments)
            {
                if (fragment.FieldType == typeof(T))
                {
                    return (T)fragment.GetValue(this);
                }
            }
            throw new Exception("cannot find fragment value of type " + typeof(T).FullName + " in object " + GetType().FullName);
        }

        public void SetFragment<T>(T value)
        {
            FieldInfo[] fragments = GetType().GetFields();
            foreach (FieldInfo fragment in fragments)
            {
                if (fragment.FieldType == typeof(T))
                {
                    fragment.SetValue(this, value);
                    return;
                }
            }
            throw new Exception("cannot find fragment value of type " + typeof(T).FullName + " in object " + GetType().FullName);
        }

        public bool TestFragment<T>(T value)
        {
            FieldInfo[] fragments = GetType().GetFields();
            foreach (FieldInfo fragment in fragments)
            {
                if (fragment.FieldType == typeof(T))
                    return Util.FlagsTest<T>((T)fragment.GetValue(this), value);
            }
            return false;
        }

        public bool HaveFragment<T>()
        {
            FieldInfo[] fragments = GetType().GetFields();
            foreach (FieldInfo fragment in fragments)
            {
                if (fragment.FieldType == typeof(T))
                    return true;
            }
            return false;
        }

        public void SetFragments(params object[] values)
        {
            FieldInfo[] fragments = GetType().GetFields();
            foreach (FieldInfo fragment in fragments)
            {
                var overrideValues = values.Where(v => v.GetType() == fragment.FieldType);
                if (overrideValues.Any())
                {
                    int value = (int)(overrideValues.Aggregate((acc, cur) => (int)acc | (int)cur));
                    fragment.SetValue(this, value);
                }
            }

            var fragmentTypes = fragments.Select(f => f.FieldType);
            var invalidTypes = values.Select(v => v.GetType()).Where(t => !fragmentTypes.Contains(t));
            if (invalidTypes.Any())
            {
                var invalidNames = invalidTypes.Select(t => t.FullName).Aggregate((current, next) => current + ", " + next);
                throw new Exception("cannot find fragment value of type " + invalidNames + " in object " + GetType().FullName);
            }
        }

        public bool AndMask(Object fragmentMask)
        {
            FieldInfo[] fragmentFields = GetType().GetFields();

            foreach (FieldInfo fragmentField in fragmentFields)
            {
                if (fragmentField.FieldType == fragmentMask.GetType())
                {
                    int targetValue = (int)fragmentField.GetValue(this);
                    int maskValue = (int)fragmentMask;
                    if ((targetValue & maskValue) != targetValue)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool IsIncludeIn(ITarget other)
        {
            if (GetType() != other.GetType())
                return false;

            int[] target1Values = GetFragmentsValue();
            int[] target2Values = other.GetFragmentsValue();

            Debug.Assert(target1Values.Length == target2Values.Length);

            for (int i = 0; i < target1Values.Length; ++i)
            {
                if ((target1Values[i] & target2Values[i]) == 0)
                    return false;
            }
            return true;
        }

        public bool IsIncludeIn(Targets others)
        {
            foreach (ITarget other in others.TargetObjects)
            {
                if (IsIncludeIn(other))
                    return true;
            }
            return false;
        }

        public int[] GetFragmentsValue()
        {
            FieldInfo[] fragmentFields = GetFragmentFieldInfo();

            int[] values = new int[fragmentFields.Length];

            for (int i = 0; i < values.Length; ++i)
            {
                FieldInfo fragmentField = fragmentFields[i];
                int fragmentValue = (int)fragmentField.GetValue(this);
                values[i] = fragmentValue;
            }
            return values;
        }

        public FieldInfo[] GetFragmentFieldInfo()
        {
            if (_fragmentFieldInfoCache == null)
            {
                _fragmentFieldInfoCache = s_cachedTypeToFragmentFieldInfos.GetOrAdd(GetType(), type =>
                {
                    List<FieldInfo> results = new List<FieldInfo>();

                    FieldInfo[] fields = GetType().GetFields();
                    foreach (FieldInfo field in fields)
                    {
                        if (field.FieldType.IsDefined(typeof(Fragment), false))
                            results.Add(field);
                    }
                    results.Sort((l, r) => l.FieldType.FullName.CompareTo(r.FieldType.FullName));

                    return results.ToArray();
                });
            }
            return _fragmentFieldInfoCache;
        }

        #region Private

        private static ConcurrentDictionary<Type, FieldInfo[]> s_cachedTypeToFragmentFieldInfos = new ConcurrentDictionary<Type, FieldInfo[]>();
        private FieldInfo[] _fragmentFieldInfoCache = null;

        private string _valueCache = null;

        #endregion
    }

    public class Targets
    {
        // Type of target object, must derive from Target
        public Type TargetType { get; private set; }

        // Target possibilities contains target with fragments bitfield,
        // will be expose for every possibles combinations of unique target value
        private List<ITarget> _targetPossibilities = new List<ITarget>();

        private Dictionary<Type, List<int>> _fragmentMasks;

        // Contain all possible unique value of target
        private List<ITarget> _targetObjects = null;

        public IEnumerable<ITarget> TargetObjects => _targetObjects;

        public IEnumerable<ITarget> TargetPossibilities => _targetPossibilities;

        public Targets()
        { }

        public Targets(Type targetType, params ITarget[] targets)
        {
            TargetType = targetType;
            AddTargets("", targets);
            BuildTargets();
        }

        private static bool IsPowerOfTwo(ulong number)
        {
            if (number == 0)
                return false;
            for (ulong power = 1; power > 0; power = power << 1)
            {
                if (power == number)
                    return true;
                if (power > number)
                    return false;
            }
            return false;
        }

        internal void Initialize(Type targetType)
        {
            if (targetType == null)
                throw new InternalError();

            if (!targetType.IsSubclassOf(typeof(ITarget)))
                throw new InternalError("target type {0} must be a subclass of {1}", targetType.FullName, typeof(ITarget).FullName);

            TargetType = targetType;

            FieldInfo[] fragments = TargetType.GetFields();

            List<Type> fragmentsType = new List<Type>();



            foreach (FieldInfo field in fragments)
            {
                if (!field.FieldType.IsEnum && !field.FieldType.IsDefined(typeof(FlagsAttribute), false))
                    throw new Error("fragment of Target Type must be enum with [Flags] attributes: " + field);

                if (fragmentsType.Contains(field.FieldType))
                    throw new Error("enum type in target must be used only once: " + field.FieldType);

                Type enumType = field.FieldType;
                FieldInfo[] enumFields = enumType.GetFields();

                for (int i = 0; i < enumFields.Length; ++i)
                {
                    // GetFields() does not gurantee order; filter out the enum's special name field
                    if (enumFields[i].Attributes.HasFlag(FieldAttributes.SpecialName))
                        continue;

                    int enumFieldValue = (int)enumFields[i].GetRawConstantValue();

                    if (enumFieldValue == 0)
                        throw new Error("0 enum field value, fragment value must 1 bit set, {0} fragment: {1}={2}", enumType.FullName, enumFields[i].Name, enumFieldValue);

                    // TODO: check if only one bit flag value
                    if (!IsPowerOfTwo((ulong)enumFieldValue))
                        throw new Error("enum field value must be power of two, ie only one bit set,{0} fragment: {1}={2}", enumType.FullName, enumFields[i].Name, enumFieldValue);

                    // make sure same value is not there twice
                    if (!field.FieldType.IsDefined(typeof(TolerateDoubleAttribute), false))
                    {
                        for (int j = 0; j < enumFields.Length; ++j)
                        {
                            // GetFields() does not gurantee order; filter out the enum's special name field
                            if (enumFields[j].Attributes.HasFlag(FieldAttributes.SpecialName))
                                continue;

                            if (i != j)
                            {
                                int jEnumFieldValue = (int)enumFields[j].GetRawConstantValue();

                                if (enumFieldValue == jEnumFieldValue)
                                {
                                    throw new Error("2 enum field with he same value found in {0} fragment: {1}={2} and {3}={4}",
                                                        enumType.FullName,
                                                        enumFields[i].Name,
                                                        enumFieldValue,
                                                        enumFields[j].Name,
                                                        jEnumFieldValue
                                    );
                                }
                            }
                        }
                    }
                }


                fragmentsType.Add(field.FieldType);
            }

            // Validate mandatory fragments
            if (!fragmentsType.Contains(typeof(DevEnv)))
                throw new Error("Mandatory fragment type \"{0}\" not found in target type \"{1}\" (fields also must be public)", typeof(DevEnv).ToString(), targetType);
            if (!fragmentsType.Contains(typeof(Platform)))
                throw new Error("Mandatory fragment type \"{0}\" not found in target type \"{1}\" (fields also must be public)", typeof(Platform).ToString(), targetType);
        }

        internal void AddTargets(string callerInfo, params ITarget[] targetsMask)
        {
            foreach (ITarget targetMask in targetsMask)
                if (TargetType != targetMask.GetType())
                    throw new Error(callerInfo + "error: Target must be all of the same type " + TargetType + " != " + targetMask.GetType() +
                        "; Are you missing base(typeof(" + targetMask.GetType() + ") in your Project class?");

            _targetPossibilities.AddRange(targetsMask);
        }

        internal void AddTargets(string callerInfo, Targets targets)
        {
            foreach (ITarget targetMask in targets._targetPossibilities)
                if (TargetType != targetMask.GetType())
                    throw new Error(callerInfo + "error: Target must be all of the same type " + TargetType + " != " + targetMask.GetType());

            _targetPossibilities.AddRange(targets._targetPossibilities);
        }

        internal bool IsFragmentValueValid(Type fragmentType, int fragmentValue)
        {
            List<int> maskValues;
            if (_fragmentMasks != null && _fragmentMasks.TryGetValue(fragmentType, out maskValues))
            {
                foreach (var maskValue in maskValues)
                {
                    if ((fragmentValue & maskValue) == fragmentValue)
                    {
                        return true;
                    }
                }
                return false;
            }

            // this type is not masked, accept it
            return true;
        }

        public void AddFragmentMask(params object[] masks)
        {
            foreach (var mask in masks)
            {
                Type maskType = mask.GetType();
                ITarget.ValidFragmentType(maskType);

                List<int> maskValues;
                if (_fragmentMasks == null || !_fragmentMasks.TryGetValue(maskType, out maskValues))
                {
                    if (_fragmentMasks == null)
                    {
                        _fragmentMasks = new Dictionary<Type, List<int>>();
                    }

                    maskValues = new List<int>();
                    _fragmentMasks.Add(maskType, maskValues);
                }

                maskValues.Add((int)mask);
            }
        }

        internal void ClearTargets()
        {
            _targetPossibilities.Clear();
        }

        internal void BuildTargets()
        {
            _targetObjects = new List<ITarget>();

            var fragments = TargetType.GetFields();

            var cachedPossibilities = _targetPossibilities.Select(tp =>
            {
                var cachedPossibility = fragments.Select(f => BuildFilteredFragmentMask(f, (int)f.GetValue(tp))).ToArray();
                return cachedPossibility;
            })
            .Where(x => x.All(y => y != 0)) // Filtered out by the _fragmentMasks
            .ToArray();

            //int[] masks;
            //BuildFragmentsMasks(fragments, out masks);

            foreach (var cachedPossibility in cachedPossibilities)
            {
                var current = new int?[fragments.Length];
                bool configValid = IncrementCurrent(fragments, cachedPossibility, current);
                while (configValid)
                {
                    GenerateConfiguration(fragments, current.Cast<int>().ToArray());
                    configValid = IncrementCurrent(fragments, cachedPossibility, current);
                }
            }
        }

        private readonly Dictionary<string, ITarget> _addedTargets = new Dictionary<string, ITarget>();

        private void GenerateConfiguration(FieldInfo[] fragments, int[] current)
        {
            ITarget target = Activator.CreateInstance(TargetType) as ITarget;

            for (int i = 0; i < fragments.Length; ++i)
                fragments[i].SetValue(target, current[i]);

            string targetKey = target.GetType().FullName + "__" + target;
            if (!_addedTargets.ContainsKey(targetKey))
            {
                _addedTargets.Add(targetKey, target);
                _targetObjects.Add(target);
            }
        }

        private int BuildFilteredFragmentMask(FieldInfo fragment, int optionalMask = int.MaxValue)
        {
            int mask = 0;

            Type enumType = fragment.FieldType;
            FieldInfo[] enumFields = enumType.GetFields();

            foreach (FieldInfo enumField in enumFields)
            {
                // GetFields() does not gurantee order; filter out the enum's special name field
                if (enumField.Attributes.HasFlag(FieldAttributes.SpecialName))
                    continue;

                int value = (int)enumField.GetRawConstantValue();

                if (IsFragmentValueValid(enumField.DeclaringType, value) && (optionalMask & value) == value)
                {
                    mask |= value;
                }
            }

            return mask;
        }

        private enum NextBitState
        {
            Initialized,
            Incremented,
            Exhausted
        }

        private static NextBitState GetNextBit(int? currentBit, int mask, out int nextBit)
        {
            Func<int, int> getNextPow2 = v =>
            {
                int p = 0;

                while (v > 0)
                {
                    p++;

                    v >>= 1;
                }

                return p;
            };

            Func<int, int, int> getNextBit = (from, current) =>
            {
                for (var bit = getNextPow2(current); bit < 32; ++bit)
                {
                    var shiftedValue = 1 << bit;

                    if ((from & shiftedValue) == shiftedValue)
                    {
                        return shiftedValue;
                    }
                }
                return 0;
            };


            if (currentBit == null)
            {
                if (mask == 0)
                {
                    nextBit = 0;
                    return NextBitState.Exhausted;
                }

                nextBit = getNextBit(mask, 0);
                return NextBitState.Initialized;
            }

            nextBit = getNextBit(mask, currentBit.Value);

            return nextBit > 0 ? NextBitState.Incremented : NextBitState.Exhausted;
        }

        private static bool IncrementCurrent(FieldInfo[] fragments, int[] masks, int?[] current)
        {
            var previousExhausted = false;

            for (var j = fragments.Length - 1; j >= 0; --j)
            {
                int next;
                NextBitState nextState = GetNextBit(current[j], masks[j], out next);

                if (nextState == NextBitState.Initialized)
                {
                    Trace.Assert(current[j] == null);                    // IsFragmentValueValid() is probably masking all enum values of fragments[j].FieldType?
                    current[j] = next;
                }
                else if (nextState == NextBitState.Incremented)
                {
                    current[j] = next;

                    if (previousExhausted)
                    {
                        for (int k = j + 1; k < fragments.Length; ++k)
                        {
                            nextState = GetNextBit(null, masks[k], out next);
                            Trace.Assert(nextState == NextBitState.Initialized);
                            current[k] = next;
                        }
                    }

                    break;
                }
                else if (nextState == NextBitState.Exhausted)
                {
                    current[j] = null;

                    if (j == 0)
                    {
                        return false;
                    }

                    previousExhausted = true;
                }
            }

            return true;
        }
    }

    public struct DevEnvRange
    {
        public DevEnvRange(IEnumerable<Project.Configuration> configurations)
        {
            DevEnv minDevEnv = 0;
            DevEnv maxDevEnv = 0;
            foreach (var conf in configurations)
            {
                DevEnv devEnv = conf.Target.GetFragment<DevEnv>();
                if (devEnv < minDevEnv || minDevEnv == 0)
                    minDevEnv = devEnv;
                if (devEnv > maxDevEnv)
                    maxDevEnv = devEnv;
            }
            MinDevEnv = minDevEnv;
            MaxDevEnv = maxDevEnv;
        }
        public DevEnv MinDevEnv;
        public DevEnv MaxDevEnv;
    }
}