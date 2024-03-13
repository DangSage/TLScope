CC = g++
CFLAGS = -Wall -Werror -std=c++17 -pedantic -g
LIBS = -lboost_unit_test_framework -lboost_serialization -lftxui-component -lftxui-dom -lftxui-screen -Iinclude -Isrc

SRC = $(wildcard src/*.cpp)
OBJ = $(patsubst src/%.cpp,bin/%.o,$(SRC))
DEP_FILES = $(patsubst src/%.cpp,bin/%.d,$(SRC))

.PHONY: all clean cleanall

all: TLScope

TLScope: main.cpp $(OBJ)
	$(CC) $(CFLAGS) -o TLScope main.cpp $(OBJ) $(LIBS)

test: test.cpp $(OBJ)
	$(CC) $(CFLAGS) -o test test.cpp $(OBJ) $(LIBS)
	./test

bin/%.o: src/%.cpp
	$(CC) $(CFLAGS) -c $< -o $@ $(LIBS)

bin/%.d: src/%.cpp
	$(CC) $(CFLAGS) -MM -MT '$(patsubst src/%.cpp,bin/%.o,$<)' $< -MF $@ $(LIBS)

-include $(DEP_FILES)

debug:
	gdb ./test

clean:
	rm -f bin/*.o bin/*.d TLScope test