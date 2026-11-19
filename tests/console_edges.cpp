#include "../src/console_edges.hpp"
#include <cassert>
#include <fstream>
#include <iostream>
#include <vector>

int main(int argc, char **argv)
{
    const int width = 2475, height = 1392, pitch = width * 4;
    std::vector<unsigned char> strips(9 * pitch, 0);
    if (argc == 2)
    {
        std::ifstream file(argv[1], std::ios::binary);
        if (!file.read(reinterpret_cast<char *>(strips.data()), strips.size()))
            return 1;
    }
    else
    {
        for (int row = 0; row < 9; ++row)
            if (row != 7)
                std::fill(strips.begin() + row * pitch + 21 * 4, strips.begin() + (row + 1) * pitch - 20 * 4, 72);
    }
    auto scan = [&] { return findConsoleEdges(strips.data(), pitch, width, height); };
    auto crop = scan();
    std::cout << crop.x << " " << crop.y << " " << crop.w << " " << crop.h << "\n";
    assert(crop.x == 21 && crop.y == 23 && crop.w == 2434 && crop.h == 1369);
    std::fill(strips.begin(), strips.end(), 0);
    assert(scan().w == width);
    std::fill(strips.begin(), strips.end(), 72);
    assert(scan().w == width);
    for (int row = 0; row < 7; ++row)
        std::fill_n(strips.begin() + row * pitch, 21 * 4, 0);
    assert(scan().w == width);
}
