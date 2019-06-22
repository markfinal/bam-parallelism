/*
Copyright (c) 2010-2019, Mark Final
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of BuildAMation nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
#include "tbb/tbb.h"

#include <vector>
#include <iostream>

struct Task
{
    Task(const int inLimit)
        :
        _limit(inLimit)
    {}

    void operator()() const
    {
        std::cout << tbb::this_tbb_thread::get_id() << "## : " << std::endl;
        for (int i = 0; i < this->_limit; ++i)
        {
            std::cout << i << " ";
        }
        std::cout << std::endl;
    }

    int _limit;
};

struct Executor
{
    Executor(const std::vector<Task> &inTasks)
        :
        _tasks(inTasks)
    {}

    void operator()(const tbb::blocked_range<size_t>& inRange) const
    {
        for (size_t it = inRange.begin(); it != inRange.end(); ++it)
        {
            this->_tasks[it]();
        }
    }

    const std::vector<Task> &_tasks;

#if __cplusplus < 201103L
private:
    Executor& operator=(const Executor&); // for older compilers
#endif
};

int main()
{
    tbb::task_scheduler_init init; // defaults

    std::vector<Task> tasks;
    for (int i = 0; i < 10; ++i)
    {
        tasks.push_back(Task(i));
    }

    Executor exec(tasks);

    std::cout << tbb::this_tbb_thread::get_id() << "## Starting parallel_for..." << std::endl;
#ifdef TBB_PREVIEW_SERIAL_SUBSET
    tbb::serial::parallel_for(
        tbb::blocked_range<size_t>(0, tasks.size()),
        exec
    );
#else
    tbb::parallel_for(
        tbb::blocked_range<size_t>(0, tasks.size()),
        exec
    );
#endif
    std::cout << tbb::this_tbb_thread::get_id() << "## Finished parallel_for" << std::endl;

    return 0;
}
