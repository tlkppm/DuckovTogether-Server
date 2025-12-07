cmake_minimum_required(VERSION 3.16)
project(duckov_guard VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(MSVC)
    add_compile_options(/W4 /O2 /GL /GS /DYNAMICBASE /NXCOMPAT)
    add_link_options(/LTCG /OPT:REF /OPT:ICF)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS)
else()
    add_compile_options(-Wall -Wextra -O3 -fPIC -fstack-protector-strong)
endif()

add_library(duckov_guard SHARED
    src/duckov_guard.cpp
)

target_include_directories(duckov_guard PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

if(WIN32)
    set_target_properties(duckov_guard PROPERTIES
        OUTPUT_NAME "duckov_guard"
        SUFFIX ".dll"
    )
endif()

install(TARGETS duckov_guard
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
)

install(FILES include/duckov_guard.h
    DESTINATION include
)
