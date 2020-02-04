#region License
// Copyright (c) 2010-2019, Mark Final
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
        C.PreprocessedFile
    {
        protected override void
        Init()
        {
            base.Init();
            this.RegisterGeneratedFile(
                PreprocessedFileKey,
                this.CreateTokenizedString("$(packagebuilddir)/$(config)/tbb.def"),
                true
            );
            this.PrivatePatch(settings =>
            {
                var preprocessor = settings as C.ICommonPreprocessorSettings;
                preprocessor.TargetLanguage = C.ETargetLanguage.Cxx;
            });
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
    class SDK :
        C.SDKTemplate
    {
        protected override Bam.Core.TypeArray LibraryModuleTypes { get; } = new Bam.Core.TypeArray(
            typeof(ThreadBuildingBlocks)
        );

        protected override void
        Init()
        {
            base.Init();

            var exportsFiles = this.Find<PreprocessExportFile>();
            foreach (var exports in exportsFiles)
            {
                (exports as Publisher.CollatedObject).Ignore = true;
            }
        }
    }

    [Bam.Core.ModuleGroup("Thirdparty/TBB")]
    class ThreadBuildingBlocks :
        C.Cxx.DynamicLibrary,
        C.IPublicHeaders
    {
        private Bam.Core.TokenizedString includeSourceRootDir;
        Bam.Core.TokenizedString C.IPublicHeaders.SourceRootDirectory => includeSourceRootDir;

        Bam.Core.StringArray C.IPublicHeaders.PublicHeaderPaths { get; } = new Bam.Core.StringArray(
            "**.h"
        );

        protected override void
        Init()
        {
            this.includeSourceRootDir = this.CreateTokenizedString("$(packagedir)/include");

            // set the version BEFORE the parent Init() in order to exclude the SharedObject name symlink
            // since that would be the name of the real shared object here
            //this.SetSemanticVersion(2); // see include/tbb/tbb_stddef.h, TBB_COMPATIBLE_INTERFACE_VERSION

            base.Init();

            this.Macros.FromName(Bam.Core.ModuleMacroNames.OutputName).SetVerbatim("tbb");

            var headers = this.CreateHeaderCollection("$(packagedir)/include/**.h");
            headers.AddFiles("$(packagedir)/src/tbb/**.h");

            var source = this.CreateCxxSourceCollection("$(packagedir)/src/tbb/*.cpp");
            source.AddFiles("$(packagedir)/src/old/concurrent_queue_v2.cpp");
            source.AddFiles("$(packagedir)/src/old/concurrent_vector_v2.cpp");
            source.AddFiles("$(packagedir)/src/old/spin_rw_mutex_v2.cpp");
            source.AddFiles("$(packagedir)/src/old/task_v2.cpp");
            source.AddFiles("$(packagedir)/src/rml/client/rml_tbb.cpp");

            // some assembly required
            if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
            {
                if (this.BitDepth == C.EBit.SixtyFour)
                {
                    this.CreateAssemblerSourceCollection("$(packagedir)/src/tbb/intel64-masm/*.asm");
                }
                else
                {
                    this.CreateAssemblerSourceCollection("$(packagedir)/src/tbb/ia32-masm/*.asm");
                }
            }

            var versionString = Bam.Core.Graph.Instance.FindReferencedModule<VersionStringVer>();
            source.DependsOn(versionString);
            source.UsePublicPatches(versionString);
            /*
            this.DependsOn(versionString);
            */

            source.PrivatePatch(settings =>
            {
                var preprocessor = settings as C.ICommonPreprocessorSettings;

                // build the library
                preprocessor.PreprocessorDefines.Add("__TBB_BUILD", "1");

                // use the public headers
                preprocessor.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/include"));

                // and some internal headers
                preprocessor.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/src"));

                // specify which threading platform is needed
                if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
                {
                    preprocessor.PreprocessorDefines.Add("USE_WINTHREAD");
                }
                else
                {
                    preprocessor.PreprocessorDefines.Add("USE_PTHREAD");
                }

                if (settings is C.ICommonCompilerSettings compiler)
                {
                    compiler.WarningsAsErrors = false;
                }
                if (settings is C.ICxxOnlyCompilerSettings cxxCompiler)
                {
                    cxxCompiler.LanguageStandard = C.Cxx.ELanguageStandard.Cxx11;
                    cxxCompiler.StandardLibrary = C.Cxx.EStandardLibrary.libcxx;
                    cxxCompiler.ExceptionHandler = C.Cxx.EExceptionHandler.Asynchronous;
                }

                if (settings is VisualCCommon.ICommonCompilerSettings vcCompiler)
                {
                    vcCompiler.WarningLevel = VisualCCommon.EWarningLevel.Level4;
                }
                if (settings is ClangCommon.ICommonCompilerSettings clangCompiler)
                {
                    clangCompiler.AllWarnings = true;
                    clangCompiler.ExtraWarnings = true;
                    clangCompiler.Pedantic = true;

                    // still needed, even with the exported symbols file
                    /*
                     * Undefined symbols for architecture x86_64:
  "tbb::interface5::internal::task_base::destroy(tbb::task&)", referenced from:
      tbb::flow::interface10::graph::~graph() in main.o
                     */
                    clangCompiler.Visibility = ClangCommon.EVisibility.Default;
                }
                if (settings is GccCommon.ICommonCompilerSettings gccCompiler)
                {
                    gccCompiler.AllWarnings = true;
                    gccCompiler.ExtraWarnings = true;
                    gccCompiler.Pedantic = true;

                    // still needed, even with the exported symbols file
                    /*
                     * Undefined symbols for architecture x86_64:
  "tbb::interface5::internal::task_base::destroy(tbb::task&)", referenced from:
      tbb::flow::interface10::graph::~graph() in main.o
                     */
                    gccCompiler.Visibility = GccCommon.EVisibility.Default;
                }

                /*
                preprocessor.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/src"));

                preprocessor.PreprocessorDefines.Add("TBB_USE_EXCEPTIONS");
                if (this.BuildEnvironment.Configuration.HasFlag(Bam.Core.EConfiguration.Debug))
                {
                    if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.NotWindows))
                    {
                        // on Windows, this will warn at compile time unless if you have a debug CRT selected
                        preprocessor.PreprocessorDefines.Add("TBB_USE_DEBUG");
                    }
                    preprocessor.PreprocessorDefines.Add("TBB_USE_ASSERT");
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

                    var compiler = settings as C.ICommonCompilerSettings;
                    compiler.DisableWarnings.AddUnique("keyword-macro");

                    var clangMeta = Bam.Core.Graph.Instance.PackageMetaData<Clang.MetaData>("Clang");
                    if (null != clangMeta)
                    {
                        if (clangMeta.ToolchainVersion.AtLeast(ClangCommon.ToolchainVersion.Xcode_10))
                        {
                            compiler.DisableWarnings.AddUnique("unused-private-field"); // this might have to go public, as it's in a public header
                        }
                    }
                }

                if (settings is GccCommon.ICommonCompilerSettings gccCompiler)
                {
                    gccCompiler.AllWarnings = true;
                    gccCompiler.ExtraWarnings = true;
                    gccCompiler.Pedantic = false;
                    // still needed, even with an version script, otherwise symbols are missing
                    gccCompiler.Visibility = GccCommon.EVisibility.Default;

                    var compiler = settings as C.ICommonCompilerSettings;
                    compiler.DisableWarnings.AddUnique("parentheses");
                }
                */
            });

            /*
            this.PublicPatch((settings, appliedTo) =>
            {
                if (settings is C.ICommonPreprocessorSettings preprocessor)
                {
                    preprocessor.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/include"));
                }
                if (settings is GccCommon.ICommonCompilerSettings gccCompiler)
                {
                    var gccMetaData = Bam.Core.Graph.Instance.PackageMetaData<Gcc.MetaData>("Gcc");
                    if (null != gccMetaData)
                    {
                        if (gccMetaData.ToolchainVersion.AtLeast(GccCommon.ToolchainVersion.GCC_9))
                        {
                            var compiler = settings as C.ICommonCompilerSettings;
                            compiler.DisableWarnings.AddUnique("class-memaccess");
                        }
                    }
                }
            });
            */

            this.PrivatePatch(settings =>
            {
                if (settings is C.ICxxOnlyLinkerSettings cxxLinker)
                {
                    cxxLinker.StandardLibrary = C.Cxx.EStandardLibrary.libcxx;
                }
            });

            if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
            {
                var preprocessedExportFile = Bam.Core.Module.Create<PreprocessExportFile>(preInitCallback: module =>
                {
                    string defFilename = "win64-tbb-export.def";
                    if (this.BitDepth == C.EBit.ThirtyTwo)
                    {
                        defFilename = "win32-tbb-export.def";
                    }

                    (module as C.IRequiresSourceModule).Source = Bam.Core.Module.Create<C.SourceFile>(preInitCallback: srcModule =>
                    {
                        srcModule.InputPath = this.CreateTokenizedString($"$(packagedir)/src/tbb/{defFilename}");
                    });
                });
                source.DependsOn(preprocessedExportFile);

                preprocessedExportFile.PrivatePatch(settings =>
                {
                    if (settings is C.ICommonPreprocessorSettings preprocessor)
                    {
                        preprocessor.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/include"));
                    }
                });

                this.PrivatePatch(settings =>
                {
                    if (settings is C.ICommonLinkerSettingsWin winLinker)
                    {
                        winLinker.ExportDefinitionFile = preprocessedExportFile.GeneratedPaths[PreprocessExportFile.PreprocessedFileKey];
                    }
                });
            }
            else if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Linux))
            {
                var preprocessedExportFile = Bam.Core.Module.Create<PreprocessExportFile>(preInitCallback: module =>
                {
                    string defFilename = "lin64-tbb-export.def";
                    if (this.BitDepth == C.EBit.ThirtyTwo)
                    {
                        defFilename = "lin32-tbb-export.def";
                    }

                    (module as C.IRequiresSourceModule).Source = Bam.Core.Module.Create<C.SourceFile>(preInitCallback: srcModule =>
                    {
                        srcModule.InputPath = this.CreateTokenizedString($"$(packagedir)/src/tbb/{defFilename}");
                    });
                });
                source.DependsOn(preprocessedExportFile);

                preprocessedExportFile.PrivatePatch(settings =>
                {
                    if (settings is C.ICommonPreprocessorSettings preprocessor)
                    {
                        preprocessor.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/include"));
                    }
                });

                this.PrivatePatch(settings =>
                {
                    if (settings is C.ICommonLinkerSettingsLinux linuxLinker)
                    {
                        linuxLinker.VersionScript = preprocessedExportFile.GeneratedPaths[PreprocessExportFile.PreprocessedFileKey];
                        linuxLinker.SharedObjectName = this.CreateTokenizedString("$(dynamicprefix)$(OutputName).so.2"); // see SONAME_SUFFIX in source tree
                    }
                    if (settings is C.ICommonLinkerSettings linker)
                    {
                        linker.Libraries.AddUnique("-lpthread");
                        linker.Libraries.AddUnique("-ldl");
                    }
                });
            }
            else if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.OSX))
            {
                var preprocessedExportFile = Bam.Core.Module.Create<PreprocessExportFile>(preInitCallback: module =>
                {
                    string defFilename = "mac64-tbb-export.def";
                    if (this.BitDepth == C.EBit.ThirtyTwo)
                    {
                        defFilename = "mac32-tbb-export.def";
                    }

                    (module as C.IRequiresSourceModule).Source = Bam.Core.Module.Create<C.SourceFile>(preInitCallback: srcModule =>
                    {
                        srcModule.InputPath = this.CreateTokenizedString($"$(packagedir)/src/tbb/{defFilename}");
                    });
                });
                source.DependsOn(preprocessedExportFile);

                preprocessedExportFile.PrivatePatch(settings =>
                {
                    if (settings is C.ICommonPreprocessorSettings preprocessor)
                    {
                        preprocessor.IncludePaths.AddUnique(this.CreateTokenizedString("$(packagedir)/include"));
                    }
                });

                this.PrivatePatch(settings =>
                {
                    if (settings is ClangCommon.ICommonLinkerSettings clangLinker)
                    {
                        clangLinker.ExportedSymbolList = preprocessedExportFile.GeneratedPaths[PreprocessExportFile.PreprocessedFileKey];
                    }
                });
            }
        }
    }

    [Bam.Core.ModuleGroup("Thirdparty/TBB/tests")]
    abstract class TBBTest :
        C.Cxx.ConsoleApplication
    {
        protected C.Cxx.ObjectFileCollection Source { get; private set; }

        protected override void
        Init()
        {
            base.Init();

            this.Source = this.CreateCxxSourceCollection();
            this.Source.CompileAgainstSDK<SDK>();
            this.LinkAgainstSDK<SDK>();

            this.Source.PrivatePatch(settings =>
            {
                var preprocess = settings as C.ICommonPreprocessorSettings;
                if (this.BuildEnvironment.Platform.Includes(Bam.Core.EPlatform.Windows))
                {
                    preprocess.PreprocessorDefines.Add("USE_WINTHREAD");
                }
                else
                {
                    preprocess.PreprocessorDefines.Add("USE_PTHREAD");
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

                if (settings is GccCommon.ICommonCompilerSettings gccCompiler)
                {
                    gccCompiler.AllWarnings = true;
                    gccCompiler.ExtraWarnings = true;
                    gccCompiler.Pedantic = false;
                }
            });

            this.PrivatePatch(settings =>
            {
                if (settings is C.ICxxOnlyLinkerSettings cxxLinker)
                {
                    cxxLinker.StandardLibrary = C.Cxx.EStandardLibrary.libcxx;
                }
                if (settings is C.ICommonLinkerSettingsLinux linuxLinker)
                {
                    linuxLinker.CanUseOrigin = true;
                    linuxLinker.RPath.AddUnique("$ORIGIN");

                    var linker = settings as C.ICommonLinkerSettings;
                    linker.Libraries.AddUnique("-lpthread");
                    linker.Libraries.AddUnique("-ldl");
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
            Init()
            {
                base.Init();
                this.Source.AddFiles("$(packagedir)/src/test/test_atomic.cpp");
            }
        }

        class Mutex :
            TBBTest
        {
            protected override void
            Init()
            {
                base.Init();
                this.Source.AddFiles("$(packagedir)/src/test/test_mutex.cpp");
            }
        }

        class ParallelFor :
            TBBTest
        {
            protected override void
            Init()
            {
                base.Init();
                this.Source.AddFiles("$(packagedir)/src/test/test_parallel_for.cpp");
            }
        }

        class Version :
            TBBTest
        {
            protected override void
            Init()
            {
                base.Init();
                this.Source.AddFiles("$(packagedir)/src/test/test_tbb_version.cpp");
            }
        }

        [Bam.Core.ModuleGroup("Thirdparty/TBB/tests")]
        sealed class TBBTests :
            Publisher.Collation
        {
            protected override void
            Init()
            {
                base.Init();

                this.SetDefaultMacrosAndMappings(EPublishingType.ConsoleApplication);

                this.IncludeAllModulesInNamespace("tbb.tests", C.Cxx.ConsoleApplication.ExecutableKey);
            }
        }
    }
}
