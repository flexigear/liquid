cmake_minimum_required(VERSION 3.20)

project(SPlisHSPlasHUnityBridge LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(SPLISH_ROOT "D:/myWorkSpace/appProjects/liquid/.experiments/SPlisHSPlasH")
set(SPLISH_BUILD "${SPLISH_ROOT}/build")
set(SPLISH_LIB_DIR "${SPLISH_BUILD}/lib/Release")
set(PBD_LIB_DIR "${SPLISH_BUILD}/extern/install/PositionBasedDynamics/lib")

add_library(SPlisHSPlasHUnityBridge SHARED
    src/SPlisHSPlasHUnityBridge.cpp
)

target_include_directories(SPlisHSPlasHUnityBridge PRIVATE
    "${SPLISH_ROOT}/extern/glfw/include"
    "${SPLISH_ROOT}/extern/glfw/src/include"
    "${SPLISH_ROOT}/extern/glfw/src/../deps"
    "${SPLISH_ROOT}/extern/imgui"
    "${SPLISH_ROOT}/extern/nfd/include"
    "${SPLISH_ROOT}/extern/eigen"
    "${SPLISH_BUILD}/extern/install/NeighborhoodSearch/include"
    "${SPLISH_BUILD}/extern/install/GenericParameters/include"
    "${SPLISH_BUILD}/extern/install/Discregrid/include"
    "${SPLISH_ROOT}"
    "${SPLISH_ROOT}/extern/zlib/src"
    "${SPLISH_BUILD}/extern/install/PositionBasedDynamics/include"
)

target_compile_definitions(SPlisHSPlasHUnityBridge PRIVATE
    WIN32
    _WINDOWS
    NDEBUG
    USE_AVX
    USE_PERFORMANCE_OPTIMIZATION
    USE_THIRD_PARTY_METHODS
    _CRT_SECURE_NO_DEPRECATE
    EIGEN_DISABLE_UNALIGNED_ARRAY_ASSERT
    _SILENCE_STDEXT_ARR_ITERS_DEPRECATION_WARNING
    IMGUI_IMPL_OPENGL_LOADER_GLAD
    USE_NFD_FILE_DIALOG
    CMAKE_INTDIR="Release"
)

if (MSVC)
    target_compile_options(SPlisHSPlasHUnityBridge PRIVATE /openmp /arch:AVX2 /fp:fast)
    set_property(TARGET SPlisHSPlasHUnityBridge PROPERTY
        MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")
endif()

target_link_directories(SPlisHSPlasHUnityBridge PRIVATE
    "${SPLISH_LIB_DIR}"
    "${PBD_LIB_DIR}"
    "${SPLISH_BUILD}/extern/install/NeighborhoodSearch/lib"
    "${SPLISH_BUILD}/extern/install/Discregrid/lib"
)

target_link_libraries(SPlisHSPlasHUnityBridge PRIVATE
    SimulatorBase
    SPlisHSPlasH
    Utilities
    partio
    tinyexpr
    MD5
    zlib
    glfw3
    imgui
    nfd
    CompactNSearch
    Discregrid
    PositionBasedDynamics
    Simulation
    Utils
    opengl32
    glu32
)

set_target_properties(SPlisHSPlasHUnityBridge PROPERTIES
    OUTPUT_NAME "SPlisHSPlasHUnityBridge"
)
