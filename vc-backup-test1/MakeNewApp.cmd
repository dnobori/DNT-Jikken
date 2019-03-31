@echo off
if "%1" == "" goto LABEL_USAGE
if "%2" == "" goto LABEL_USAGE

perl -pi -e 's/nativeapp/%2/g' main.c
perl -pi -e 's/nativeapp/%2/g' main.h
perl -pi -e 's/nativeapp/%2/g' Makefile
perl -pi -e 's/nativeapp/%2/g' nativeapp.h
perl -pi -e 's/nativeapp/%2/g' nativeapp.sln
perl -pi -e 's/nativeapp/%2/g' nativeapp.vcxproj
perl -pi -e 's/nativeapp/%2/g' nativeapp.vcxproj.filters
perl -pi -e 's/nativeapp/%2/g' nativeapp.vcxproj.user
perl -pi -e 's/nativeapp/%2/g' nativeapp_types.h
perl -pi -e 's/nativeapp/%2/g' temp.c
perl -pi -e 's/nativeapp/%2/g' temp.h

perl -pi -e 's/NATIVEAPP/%1/g' main.c
perl -pi -e 's/NATIVEAPP/%1/g' main.h
perl -pi -e 's/NATIVEAPP/%1/g' Makefile
perl -pi -e 's/NATIVEAPP/%1/g' nativeapp.h
perl -pi -e 's/NATIVEAPP/%1/g' nativeapp.sln
perl -pi -e 's/NATIVEAPP/%1/g' nativeapp.vcxproj
perl -pi -e 's/NATIVEAPP/%1/g' nativeapp.vcxproj.filters
perl -pi -e 's/NATIVEAPP/%1/g' nativeapp.vcxproj.user
perl -pi -e 's/NATIVEAPP/%1/g' nativeapp_types.h
perl -pi -e 's/NATIVEAPP/%1/g' temp.c
perl -pi -e 's/NATIVEAPP/%1/g' temp.h

mv nativeapp.h %2.h
mv nativeapp.sln %2.sln
mv nativeapp.vcxproj %2.vcxproj
mv nativeapp.vcxproj.filters %2.vcxproj.filters
mv nativeapp.vcxproj.user %2.vcxproj.user
mv nativeapp_types.h %2_types.h

del *.bak


goto LABEL_END

:LABEL_USAGE
echo Usage: MakeNewApp APPNAME appname

:LABEL_END


