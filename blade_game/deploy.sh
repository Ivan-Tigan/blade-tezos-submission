name=$1
filename=$1
shift 
deploy_target(){
(
    channel=$1
    if [[ $channel =~ "html" ]] || [[ $channel =~ "HTML" ]] then
        filename="index.html"
    fi

    if [[ $channel =~ "Windows" ]] || [[ $channel =~ "windows" ]] then
    	filename="$name.exe"
    fi
    if [[ $channel =~ "Linux" ]] || [[ $channel =~ "linux" ]] then
	filename="$name.x86_64"
    fi
    if [[ $channel =~ "Mac" ]] || [[ $channel =~ "mac" ]] then
	filename="$name.zip"
    fi
    mkdir build
    mkdir build/$channel
    godotsharp --export "$channel" build/$channel/$filename
    mkdir zipbuild
    mkdir zipbuild/$channel
    zip -r zipbuild/$channel/$name.zip build/$channel/.
    butler push zipbuild/$channel/$name.zip ivan-tigan/${name//_/-}:$channel
#    if [[ $channel =~ "Windows" ]] || [[ $channel =~ "windows" ]] then
#        scp -r zipbuild/windows root@recklessgame.net:/releases/windows
#    fi

)
}
for var in $@
do 
	deploy_target $var 
done



