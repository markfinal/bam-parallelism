#region License
// Copyright (c) 2010-2018, Mark Final
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
// * Redistributions of source code must retain the above copyright notice, this
//   list of conditions and the following disclaimer.
//
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// * Neither the name of BuildAMation nor the names of its
//   contributors may be used to endorse or promote products derived from
//   this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion // License
using Bam.Core;
namespace tbb
{
    class PreprocessExportFile :
        C.ObjectFileBase
    {
        protected override bool RequiresHeaderEvaluation => true;

        protected override void
        Init(
            Bam.Core.Module parent)
        {
            base.Init(parent);
            this.Tool = C.DefaultToolchain.C_Compiler(this.BitDepth);
            this.RegisterGeneratedFile(
                ObjectFileKey,
                this.CreateTokenizedString("$(packagebuilddir)/$(config)/tbb.def")
            );
            this.PrivatePatch(settings =>
            {
                var compiler = settings as C.ICommonCompilerSettings;
                compiler.PreprocessOnly = true;
                compiler.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/include"));

                if (settings is ClangCommon.ICommonCompilerSettings)
                {
                    compiler.DisableWarnings.AddUnique("invalid-pp-token");
                }
            });
        }

        protected override void
        EvaluateInternal()
        {
        }

        protected override void
        ExecuteInternal(
            Bam.Core.ExecutionContext context)
        {
            switch (Bam.Core.Graph.Instance.Mode)
            {
#if D_PACKAGE_MAKEFILEBUILDER
                case "MakeFile":
                    MakeFileBuilder.Support.Add(this);
                    break;
#endif

#if D_PACKAGE_NATIVEBUILDER
                case "Native":
                    NativeBuilder.Support.RunCommandLineTool(this, context);
                    break;
#endif

#if D_PACKAGE_VSSOLUTIONBUILDER
                case "VSSolution":
                    VSSolutionBuilder.Support.AddCustomBuildStepForCommandLineTool(
                        this,
                        this.InputPath,
                        "Preprocessing",
                        true
                    );
                    break;
#endif

#if D_PACKAGE_XCODEBUILDER
                case "Xcode":
                    {
                        XcodeBuilder.Support.AddPreBuildStepForCommandLineTool(
                            this,
                            out XcodeBuilder.Target target,
                            out XcodeBuilder.Configuration configuration,
                            XcodeBuilder.FileReference.EFileType.GLSLShaderSource,
                            true,
                            false,
                            outputPaths: new Bam.Core.TokenizedStringArray(this.InputPath)
                        );
                    }
                    break;
#endif

                default:
                    throw new System.NotImplementedException();
            }
        }
    }

    [Bam.Core.ModuleGroup("Thirdparty/TBB")]
    class VersionStringVer :
        C.ProceduralHeaderFile
    {
        protected override Bam.Core.TokenizedString OutputPath => this.CreateTokenizedString("$(packagebuilddir)/$(config)/version_string.ver");
        protected override string Contents
        {
            get
            {
                var content = new System.Text.StringBuilder();
                content.AppendLine("#define __TBB_VERSION_STRINGS(N)");
                return content.ToString();
            }
        }
    }

    [Bam.Core.ModuleGroup("Thirdparty/TBB")]
    class ThreadBuildingBlocks :
        C.Cxx.DynamicLibrary
    {
        protected override void
        Init(
            Bam.Core.Module parent)
        {
            base.Init(parent);

            var headers = this.CreateHeaderContainer("$(packagedir)/include/**.h");
            headers.AddFiles("$(packagedir)/src/tbb/**.h");

            var source = this.CreateCxxSourceContainer("$(packagedir)/src/tbb/*.cpp");
            source.AddFiles("$(packagedir)/src/rml/client/rml_tbb.cpp");

            var versionString = Bam.Core.Graph.Instance.FindReferencedModule<VersionStringVer>();
            source.DependsOn(versionString);
            this.DependsOn(versionString);
            source.UsePublicPatches(versionString);

            source.PrivatePatch(settings =>
            {
                var compiler = settings as C.ICommonCompilerSettings;
                compiler.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/src"));

                compiler.PreprocessorDefines.Add("TBB_USE_EXCEPTIONS");
                compiler.PreprocessorDefines.Add("__TBB_BUILD", "1");
                if (this.BuildEnvironment.Configuration.HasFlag(Bam.Core.EConfiguration.Debug))
                {
                    if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.NotWindows))
                    {
                        // on Windows, this will warn at compile time unless if you have a debug CRT selected
                        compiler.PreprocessorDefines.Add("TBB_USE_DEBUG");
                    }
                    compiler.PreprocessorDefines.Add("TBB_USE_ASSERT");
                }
                if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
                {
                    compiler.PreprocessorDefines.Add("USE_WINTHREAD");
                }
                else
                {
                    compiler.PreprocessorDefines.Add("USE_PTHREAD");
                }

                var cxxCompiler = settings as C.ICxxOnlyCompilerSettings;
                cxxCompiler.LanguageStandard = C.Cxx.ELanguageStandard.Cxx11;
                cxxCompiler.StandardLibrary = C.Cxx.EStandardLibrary.libcxx;
                cxxCompiler.ExceptionHandler = C.Cxx.EExceptionHandler.Asynchronous;

                if (settings is ClangCommon.ICommonCompilerSettings clangCompiler)
                {
                    clangCompiler.AllWarnings = true;
                    clangCompiler.ExtraWarnings = true;
                    clangCompiler.Pedantic = true;
                    clangCompiler.Visibility = ClangCommon.EVisibility.Default;

                    compiler.DisableWarnings.AddUnique("keyword-macro");
                }
            });

            this.PublicPatch((settings, appliedTo) =>
            {
                if (settings is C.ICommonCompilerSettings compiler)
                {
                    compiler.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/include"));
                }
            });

            this.PrivatePatch(settings =>
            {
                if (settings is C.ICxxOnlyLinkerSettings cxxLinker)
                {
                    cxxLinker.StandardLibrary = C.Cxx.EStandardLibrary.libcxx;
                }
            });

            if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
            {
                // some assembly required
                this.CreateAssemblerSourceContainer("$(packagedir)/src/tbb/intel64-masm/intel64_misc.asm");
            }

            if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
            {
                var preprocessedExportFile = Bam.Core.Module.Create<PreprocessExportFile>(preInitCallback: module =>
                {
                    module.InputPath = this.CreateTokenizedString("$(packagedir)/src/tbb/win64-tbb-export.def");
                });
                this.DependsOn(preprocessedExportFile);

                this.PrivatePatch(settings =>
                {
                    if (settings is C.ICommonLinkerSettingsWin winLinker)
                    {
                        winLinker.ExportDefinitionFile = preprocessedExportFile.GeneratedPaths[C.ObjectFileBase.ObjectFileKey];
                    }
                });
            }
            else if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Linux))
            {
                var preprocessedExportFile = Bam.Core.Module.Create<PreprocessExportFile>(preInitCallback: module =>
                {
                    module.InputPath = this.CreateTokenizedString("$(packagedir)/src/tbb/lin64-tbb-export.def");
                });
                this.DependsOn(preprocessedExportFile);
            }
            else if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.OSX))
            {
                var preprocessedExportFile = Bam.Core.Module.Create<PreprocessExportFile>(preInitCallback: module =>
                {
                    module.InputPath = this.CreateTokenizedString("$(packagedir)/src/tbb/mac64-tbb-export.def");
                });
                this.DependsOn(preprocessedExportFile);
            }
        }
    }

    [Bam.Core.ModuleGroup("Thirdparty/TBB/tests")]
    abstract class TBBTest :
        C.Cxx.ConsoleApplication
    {
        protected C.Cxx.ObjectFileCollection Source { get; private set; }

        protected override void
        Init(
            Bam.Core.Module parent)
        {
            base.Init(parent);

            this.Source = this.CreateCxxSourceContainer();
            this.CompileAndLinkAgainst<ThreadBuildingBlocks>(this.Source);

            this.Source.PrivatePatch(settings =>
            {
                var compiler = settings as C.ICommonCompilerSettings;
                if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
                {
                    compiler.PreprocessorDefines.Add("USE_WINTHREAD");
                }
                else
                {
                    compiler.PreprocessorDefines.Add("USE_PTHREAD");
                }

                var cxxCompiler = settings as C.ICxxOnlyCompilerSettings;
                cxxCompiler.LanguageStandard = C.Cxx.ELanguageStandard.Cxx11;
                cxxCompiler.StandardLibrary = C.Cxx.EStandardLibrary.libcxx;
                cxxCompiler.ExceptionHandler = C.Cxx.EExceptionHandler.Asynchronous;

                if (settings is ClangCommon.ICommonCompilerSettings clangCompiler)
                {
                    clangCompiler.AllWarnings = true;
                    clangCompiler.ExtraWarnings = true;
                    clangCompiler.Pedantic = true;
                }
            });

            this.PrivatePatch(settings =>
            {
                if (settings is C.ICxxOnlyLinkerSettings cxxLinker)
                {
                    cxxLinker.StandardLibrary = C.Cxx.EStandardLibrary.libcxx;
                }
            });
        }
    }

    namespace tests
    {
        class Atomic :
            TBBTest
        {
            protected override void
            Init(
                Bam.Core.Module parent)
            {
                base.Init(parent);
                this.Source.AddFiles("$(packagedir)/src/test/test_atomic.cpp");
            }
        }

        class Mutex :
            TBBTest
        {
            protected override void
            Init(
                Bam.Core.Module parent)
            {
                base.Init(parent);
                this.Source.AddFiles("$(packagedir)/src/test/test_mutex.cpp");
            }
        }

        class ParallelFor :
            TBBTest
        {
            protected override void
            Init(
                Bam.Core.Module parent)
            {
                base.Init(parent);
                this.Source.AddFiles("$(packagedir)/src/test/test_parallel_for.cpp");
            }
        }

        class Version :
            TBBTest
        {
            protected override void
            Init(
                Bam.Core.Module parent)
            {
                base.Init(parent);
                this.Source.AddFiles("$(packagedir)/src/test/test_tbb_version.cpp");
            }
        }

        [Bam.Core.ModuleGroup("Thirdparty/TBB/tests")]
        sealed class TBBTests :
            Publisher.Collation
        {
            protected override void
            Init(
                Bam.Core.Module parent)
            {
                base.Init(parent);

                this.SetDefaultMacrosAndMappings(EPublishingType.ConsoleApplication);

                this.IncludeAllModulesInNamespace("tbb.tests", C.Cxx.ConsoleApplication.ExecutableKey);
            }
        }
    }
}
